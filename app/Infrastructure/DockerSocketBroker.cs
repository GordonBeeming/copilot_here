using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using CopilotHere.Commands.DockerBroker;

namespace CopilotHere.Infrastructure;

/// <summary>
/// Where the broker listens for incoming Docker API requests from the container.
/// Linux/macOS: a Unix Domain Socket the container bind-mounts at /var/run/docker.sock.
/// Windows: an ephemeral TCP loopback port, reached via host.docker.internal.
/// </summary>
public sealed record BrokerListenEndpoint
{
  public string? UnixPath { get; init; }
  public IPEndPoint? TcpEndpoint { get; init; }
  public bool IsUnix => UnixPath is not null;
  public bool IsTcp => TcpEndpoint is not null;

  public static BrokerListenEndpoint Unix(string path) => new() { UnixPath = path };
  public static BrokerListenEndpoint Tcp(IPAddress address, int port) => new() { TcpEndpoint = new IPEndPoint(address, port) };
}

/// <summary>
/// Host-side broker that mediates Docker API calls between the workload container
/// and the host runtime daemon. Each session creates one broker, owned by the
/// copilot_here host process. The container sees a normal Docker socket; the host
/// decides which API endpoints are forwarded to the real daemon.
///
/// HTTP/1.1 framing is intentionally hand-rolled (no Kestrel) to keep the binary
/// AOT-friendly. After the request line is approved, request and response bytes
/// are spliced verbatim — that handles content-length, chunked transfer, and
/// Upgrade-based hijacking (exec/attach) without further parsing.
/// </summary>
public sealed class DockerSocketBroker : IAsyncDisposable
{
  private readonly DockerBrokerConfig _config;
  private readonly string _hostSocketPath;
  private readonly BrokerListenEndpoint _listen;
  private readonly string? _logPath;

  private Socket? _unixListener;
  private TcpListener? _tcpListener;
  private CancellationTokenSource? _cts;
  private Task? _acceptLoop;
  private bool _disposed;
  private IPEndPoint? _boundTcpEndpoint;

  public BrokerListenEndpoint Listen => _listen;
  public DockerBrokerConfig Config => _config;

  /// <summary>
  /// When set, the broker injects this value into HostConfig.NetworkMode for
  /// every POST /containers/create request whose body has no explicit network.
  /// Used in airlock + DinD mode so spawned siblings join the airlocked
  /// network and remain reachable from the workload. Standard --dind mode
  /// leaves this null.
  /// </summary>
  public string? SiblingNetworkName { get; set; }

  /// <summary>
  /// Once <see cref="StartAsync"/> has bound a TCP listener, this returns the
  /// real endpoint (with the OS-assigned port if the caller asked for port 0).
  /// Null for Unix-domain listeners or before Start.
  /// </summary>
  public IPEndPoint? BoundTcpEndpoint => _boundTcpEndpoint;

  /// <summary>
  /// The path the workload container should bind-mount as /var/run/docker.sock,
  /// or null if this broker is using the TCP transport.
  /// </summary>
  public string? UnixSocketPath => _listen.UnixPath;

  public DockerSocketBroker(
    DockerBrokerConfig config,
    string hostSocketPath,
    BrokerListenEndpoint listen,
    string? logPath = null)
  {
    _config = config ?? throw new ArgumentNullException(nameof(config));
    _hostSocketPath = hostSocketPath ?? throw new ArgumentNullException(nameof(hostSocketPath));
    _listen = listen ?? throw new ArgumentNullException(nameof(listen));
    _logPath = logPath;
  }

  /// <summary>
  /// Starts the broker. Binds the listener and kicks off the accept loop on a
  /// background task. Returns immediately so the caller can spawn the workload
  /// container while the broker handles connections.
  /// </summary>
  public Task StartAsync(CancellationToken ct)
  {
    if (_disposed) throw new ObjectDisposedException(nameof(DockerSocketBroker));
    if (_acceptLoop is not null) throw new InvalidOperationException("Broker already started");

    _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

    if (_listen.IsUnix)
    {
      var path = _listen.UnixPath!;
      // Best-effort cleanup of any leftover socket file from a crashed prior session.
      try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }

      _unixListener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
      _unixListener.Bind(new UnixDomainSocketEndPoint(path));
      _unixListener.Listen(32);

      // Restrict the socket to the host user; the workload container runs with the
      // same UID/GID via PUID/PGID so it can still connect.
      try
      {
        if (!OperatingSystem.IsWindows())
        {
          File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite);
        }
      }
      catch
      {
        // Mode restriction is best-effort; the listener still works without it.
      }
    }
    else if (_listen.IsTcp)
    {
      _tcpListener = new TcpListener(_listen.TcpEndpoint!);
      _tcpListener.Start();
      _boundTcpEndpoint = (IPEndPoint)_tcpListener.LocalEndpoint;
    }
    else
    {
      throw new InvalidOperationException("BrokerListenEndpoint must specify UnixPath or TcpEndpoint");
    }

    _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
    return Task.CompletedTask;
  }

  private async Task AcceptLoopAsync(CancellationToken ct)
  {
    while (!ct.IsCancellationRequested)
    {
      try
      {
        Socket accepted;
        if (_unixListener is not null)
        {
          accepted = await _unixListener.AcceptAsync(ct).ConfigureAwait(false);
        }
        else
        {
          accepted = await _tcpListener!.AcceptSocketAsync(ct).ConfigureAwait(false);
        }
        _ = HandleConnectionAsync(accepted, ct);
      }
      catch (OperationCanceledException)
      {
        break;
      }
      catch (ObjectDisposedException)
      {
        break;
      }
      catch (Exception ex)
      {
        DebugLogger.Log($"DockerSocketBroker accept error: {ex.Message}");
      }
    }
  }

  private async Task HandleConnectionAsync(Socket clientSocket, CancellationToken ct)
  {
    Stream? upstream = null;
    try
    {
      using var clientStream = new NetworkStream(clientSocket, ownsSocket: true);

      // Buffer the request line + headers (everything up to the empty line that
      // terminates the headers). Cap at 64 KiB — Docker API request headers are
      // rarely anywhere near that size.
      const int MaxHeaderBytes = 65536;
      var headerBuf = new byte[MaxHeaderBytes];
      int totalRead = 0;
      int headersEnd = -1;
      int terminatorLength = 0;

      while (totalRead < headerBuf.Length)
      {
        var n = await clientStream.ReadAsync(headerBuf.AsMemory(totalRead, headerBuf.Length - totalRead), ct).ConfigureAwait(false);
        if (n <= 0) return;
        totalRead += n;
        headersEnd = IndexOfHeadersEnd(headerBuf, totalRead, out terminatorLength);
        if (headersEnd >= 0) break;
      }

      if (headersEnd < 0)
      {
        await WriteSimpleResponseAsync(clientStream, 431, "request headers too large", ct).ConfigureAwait(false);
        return;
      }

      // Parse the request line. The first line terminator can be \r\n or bare \n.
      var firstLineEnd = IndexOfLineTerminator(headerBuf, totalRead);
      if (firstLineEnd <= 0)
      {
        await WriteSimpleResponseAsync(clientStream, 400, "bad request", ct).ConfigureAwait(false);
        return;
      }

      var firstLine = Encoding.ASCII.GetString(headerBuf, 0, firstLineEnd);
      var parts = firstLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
      if (parts.Length < 3)
      {
        await WriteSimpleResponseAsync(clientStream, 400, "bad request", ct).ConfigureAwait(false);
        return;
      }

      var method = parts[0];
      var rawTarget = parts[1];
      var canonicalPath = StripVersionPrefix(StripQuery(rawTarget));

      var (allowed, reason) = CheckRule(method, canonicalPath);

      if (!allowed)
      {
        LogDecision("BLOCK", method, rawTarget, reason);
        await WriteJsonErrorAsync(clientStream, 403, $"blocked by copilot_here docker broker: {reason}", ct).ConfigureAwait(false);
        return;
      }

      LogDecision("ALLOW", method, rawTarget, reason);

      // Detect HTTP Upgrade BEFORE we touch the request bytes. Docker uses
      // Upgrade-based hijacking for /containers/*/attach and /exec/*/start;
      // those connections become raw bidirectional streams after a 101
      // Switching Protocols (or 200 OK) response, and we MUST NOT inject
      // `Connection: close` into them — that header is mutually exclusive
      // with `Connection: Upgrade` and the upstream daemon will reject the
      // upgrade, leaving the container in "Created" state and the client
      // hanging forever.
      var isUpgrade = LooksLikeHijack(headerBuf, headersEnd);
      var bodyStart = headersEnd + terminatorLength;

      // Phase 2: body inspection for POST /containers/create. We buffer the
      // full body, parse it as JSON, run safety rules, and either reject the
      // request or rewrite the body (e.g. injecting NetworkMode for airlock
      // siblings). Anything else falls through to the standard splice path.
      byte[]? rewrittenBody = null;
      bool bodyAlreadyConsumed = false;
      if (!isUpgrade &&
          string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase) &&
          string.Equals(canonicalPath, "/containers/create", StringComparison.Ordinal))
      {
        var inspectionOutcome = await ReadAndInspectCreateBodyAsync(
          clientStream, headerBuf, totalRead, headersEnd, bodyStart, ct).ConfigureAwait(false);

        if (inspectionOutcome.Blocked)
        {
          LogDecision("BLOCK", method, rawTarget, inspectionOutcome.Reason);
          await WriteJsonErrorAsync(clientStream, 403, $"blocked by copilot_here docker broker: {inspectionOutcome.Reason}", ct).ConfigureAwait(false);
          return;
        }

        rewrittenBody = inspectionOutcome.RewrittenBody;
        bodyAlreadyConsumed = inspectionOutcome.FullyConsumed;
        if (rewrittenBody is not null)
        {
          LogDecision("REWRITE", method, rawTarget, inspectionOutcome.Reason);
        }
      }

      // Connect to the host runtime daemon.
      try
      {
        upstream = await ConnectUpstreamAsync(ct).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        DebugLogger.Log($"DockerSocketBroker upstream connect failed: {ex.Message}");
        await WriteJsonErrorAsync(clientStream, 502, $"docker broker could not reach the host daemon at {_hostSocketPath}", ct).ConfigureAwait(false);
        return;
      }

      if (isUpgrade)
      {
        // Hijacked endpoints (exec/attach): forward the original request
        // verbatim — don't touch Connection or any other header — then splice
        // bytes in both directions. The hijack consumes the entire TCP
        // connection, so the keep-alive-bypass concern below doesn't apply:
        // a single connection carries exactly one logical operation.
        //
        // Lifecycle: we MUST wait until the *upstream → client* direction
        // EOFs before tearing down. Waiting on Task.WhenAny was a bug —
        // for `docker run alpine echo X`, the docker CLI shuts its write
        // half immediately (it has no input for echo), so client→upstream
        // completes within microseconds. WhenAny would then dispose both
        // streams while the alpine container's stdout was still in flight,
        // and the user would see exit=0 with empty output. The right
        // signal is the upstream side: when the daemon closes the upgraded
        // socket (because the container exited), all output has been
        // delivered. Anything we still owe the client at that point is
        // already in our send buffer.
        await upstream.WriteAsync(headerBuf.AsMemory(0, totalRead), ct).ConfigureAwait(false);
        using var hijackCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var clientToUpstream = clientStream.CopyToAsync(upstream, hijackCts.Token);
        var upstreamToClient = upstream.CopyToAsync(clientStream, hijackCts.Token);
        try
        {
          await upstreamToClient.ConfigureAwait(false);
        }
        finally
        {
          // Upstream is done; client→upstream is irrelevant now. Cancel it
          // so disposal doesn't strand a background task on a closed socket.
          hijackCts.Cancel();
          try { await clientToUpstream.ConfigureAwait(false); } catch { /* expected on cancel */ }
        }
      }
      else
      {
        // Standard request/response. Rewrite the request to force
        // `Connection: close` before forwarding.
        //
        // Why: HTTP/1.1 keep-alive lets a single TCP connection carry many
        // requests. If we just spliced bytes after approving the first
        // request, every subsequent request on the same connection (e.g.
        // POST /containers/create after a benign GET /_ping) would bypass
        // the rule engine. That defeats the point of the broker.
        //
        // By stripping the client's Connection/Keep-Alive headers and
        // inserting `Connection: close`, we tell the upstream daemon to
        // close the socket after the response. The client then has to open
        // a fresh connection for its next request, and that connection goes
        // through CheckRule from scratch.
        //
        // Cost: one TCP handshake per Docker API call. Unmeasurable against
        // a local Unix socket and entirely worth the security guarantee.
        var rewrittenRequest = RewriteRequestForceClose(headerBuf, totalRead, headersEnd, bodyStart, rewrittenBody);
        await upstream.WriteAsync(rewrittenRequest, ct).ConfigureAwait(false);

        // Bidirectional splice. We've forwarded the buffered head of the
        // request, but the request body may extend beyond what we've read
        // (Content-Length larger than the initial read, or chunked transfer).
        // Without the client→upstream task, large POST bodies (e.g.
        // POST /containers/create with a JSON spec) hang the upstream daemon
        // forever waiting for body bytes that never arrive.
        //
        // The keep-alive bypass concern that originally made me drop this
        // bidirectional copy doesn't apply here: the rewritten request asked
        // for Connection: close, so the upstream sends its response and then
        // closes. As soon as upstream→client EOFs we cancel the client side
        // and dispose both streams. The client can't slip a second request
        // through onto an already-dead connection.
        //
        // When we already consumed the body during inspection (Phase 2 path),
        // there's nothing left on the client side — skip the client→upstream
        // task entirely. Otherwise the splice would block indefinitely on a
        // socket the client has nothing more to write to.
        using var spliceCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var upstreamToClient = upstream.CopyToAsync(clientStream, spliceCts.Token);
        Task? clientToUpstream = null;
        if (!bodyAlreadyConsumed)
        {
          clientToUpstream = clientStream.CopyToAsync(upstream, spliceCts.Token);
        }
        try
        {
          if (clientToUpstream is not null)
            await Task.WhenAny(clientToUpstream, upstreamToClient).ConfigureAwait(false);
          else
            await upstreamToClient.ConfigureAwait(false);
        }
        finally
        {
          // Tear down the other side of the splice once one direction has
          // finished. Without this, a stuck client→upstream task would keep
          // the broker connection alive past the response.
          spliceCts.Cancel();
        }
      }
    }
    catch (OperationCanceledException)
    {
      // Shutdown — fall through to dispose.
    }
    catch (Exception ex)
    {
      DebugLogger.Log($"DockerSocketBroker connection error: {ex.Message}");
    }
    finally
    {
      try { upstream?.Dispose(); } catch { /* ignore */ }
    }
  }

  private async Task<Stream> ConnectUpstreamAsync(CancellationToken ct)
  {
    if (_hostSocketPath.StartsWith("\\\\.\\pipe\\", StringComparison.Ordinal))
    {
      var pipeName = _hostSocketPath["\\\\.\\pipe\\".Length..];
      var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
      await pipe.ConnectAsync(5000, ct).ConfigureAwait(false);
      return pipe;
    }

    var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
    await socket.ConnectAsync(new UnixDomainSocketEndPoint(_hostSocketPath), ct).ConfigureAwait(false);
    return new NetworkStream(socket, ownsSocket: true);
  }

  // ─── Rule matching ──────────────────────────────────────────────────────────

  /// <summary>
  /// Returns (allowed, reason) for a given Docker API method + path. The path
  /// must already have its /v1.NN prefix and query string stripped.
  /// </summary>
  internal (bool Allowed, string Reason) CheckRule(string method, string path)
  {
    // Monitor mode allows everything; we still log the decision so users can audit.
    if (!string.Equals(_config.Mode, "enforce", StringComparison.OrdinalIgnoreCase))
    {
      return (true, "monitor mode");
    }

    foreach (var endpoint in _config.AllowedEndpoints)
    {
      if (!endpoint.Method.Equals(method, StringComparison.OrdinalIgnoreCase)) continue;
      if (PathMatches(endpoint.Path, path)) return (true, "matched allowlist");
    }

    return (false, $"no rule for {method} {path}");
  }

  /// <summary>
  /// Segment-aware path matcher.
  /// <list type="bullet">
  ///   <item><c>*</c> matches exactly one path segment (e.g. <c>/containers/*/start</c>
  ///         matches <c>/containers/abc123/start</c>).</item>
  ///   <item><c>**</c> matches zero or more path segments (e.g. <c>/images/**/json</c>
  ///         matches <c>/images/json</c>, <c>/images/alpine/json</c>, and
  ///         <c>/images/testcontainers/ryuk:0.14.0/json</c>). Image-related Docker
  ///         API endpoints need <c>**</c> because image names can contain literal
  ///         slashes (registry/repo:tag).</item>
  /// </list>
  /// </summary>
  internal static bool PathMatches(string pattern, string path)
  {
    if (string.IsNullOrEmpty(pattern) || pattern[0] != '/') return false;
    if (string.IsNullOrEmpty(path) || path[0] != '/') return false;

    var patternSegments = pattern[1..].Split('/');
    var pathSegments = path[1..].Split('/');

    return MatchSegments(patternSegments, 0, pathSegments, 0);
  }

  private static bool MatchSegments(string[] pattern, int pi, string[] path, int qi)
  {
    while (pi < pattern.Length)
    {
      if (pattern[pi] == "**")
      {
        // ** matches zero or more segments. If it's the last token, it greedily
        // consumes the remainder of the path. Otherwise, try every possible
        // split point and recurse.
        if (pi == pattern.Length - 1) return true;
        for (int k = qi; k <= path.Length; k++)
        {
          if (MatchSegments(pattern, pi + 1, path, k)) return true;
        }
        return false;
      }

      if (qi >= path.Length) return false;
      if (pattern[pi] != "*" && !pattern[pi].Equals(path[qi], StringComparison.Ordinal)) return false;

      pi++;
      qi++;
    }
    return qi == path.Length;
  }

  /// <summary>
  /// Strips an optional /v\d+(\.\d+)? API version prefix from a Docker API path.
  /// "/v1.43/containers/json" → "/containers/json".
  /// "/containers/json" → "/containers/json".
  /// </summary>
  internal static string StripVersionPrefix(string path)
  {
    if (path.Length < 4 || path[0] != '/' || path[1] != 'v') return path;

    int i = 2;
    while (i < path.Length && char.IsDigit(path[i])) i++;
    if (i == 2) return path; // no digits after the 'v'

    if (i < path.Length && path[i] == '.')
    {
      i++;
      int beforeMinor = i;
      while (i < path.Length && char.IsDigit(path[i])) i++;
      if (i == beforeMinor) return path; // dangling dot
    }

    if (i < path.Length && path[i] == '/')
    {
      return path[i..];
    }

    return path;
  }

  internal static string StripQuery(string target)
  {
    var q = target.IndexOf('?');
    return q >= 0 ? target[..q] : target;
  }

  // ─── Logging ────────────────────────────────────────────────────────────────

  private void LogDecision(string action, string method, string target, string reason)
  {
    if (!_config.EnableLogging || _logPath is null) return;

    try
    {
      var dir = Path.GetDirectoryName(_logPath);
      if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

      var entry = new StringBuilder(160);
      entry.Append("{\"ts\":\"");
      entry.Append(DateTime.UtcNow.ToString("o"));
      entry.Append("\",\"action\":\"");
      entry.Append(action);
      entry.Append("\",\"method\":\"");
      entry.Append(EscapeJson(method));
      entry.Append("\",\"target\":\"");
      entry.Append(EscapeJson(target));
      entry.Append("\",\"reason\":\"");
      entry.Append(EscapeJson(reason));
      entry.Append("\",\"mode\":\"");
      entry.Append(_config.Mode);
      entry.Append("\"}\n");

      File.AppendAllText(_logPath, entry.ToString());
    }
    catch
    {
      // Logging is best-effort. The session must not fail because we couldn't write to disk.
    }
  }

  private static string EscapeJson(string value)
  {
    if (string.IsNullOrEmpty(value)) return string.Empty;
    var sb = new StringBuilder(value.Length + 8);
    foreach (var c in value)
    {
      switch (c)
      {
        case '"': sb.Append("\\\""); break;
        case '\\': sb.Append("\\\\"); break;
        case '\n': sb.Append("\\n"); break;
        case '\r': sb.Append("\\r"); break;
        case '\t': sb.Append("\\t"); break;
        default:
          if (c < 0x20) sb.Append($"\\u{(int)c:x4}");
          else sb.Append(c);
          break;
      }
    }
    return sb.ToString();
  }

  // ─── Request rewriting ──────────────────────────────────────────────────────

  /// <summary>
  /// Rewrites the buffered HTTP request to force Connection: close. This is the
  /// mechanism that makes the broker re-evaluate the rule engine for every
  /// Docker API call: by closing after one response, the next call from the
  /// same client has to open a fresh connection, which re-enters HandleConnectionAsync.
  ///
  /// The buffered bytes contain: request line + headers + (optionally, the
  /// start of the body). We strip the client's Connection/Keep-Alive/Proxy-
  /// Connection headers, append `Connection: close`, and preserve any body
  /// bytes that were already in the buffer.
  ///
  /// HTTP/1.1 line terminators can be `\r\n` or bare `\n` (RFC 7230 §3.5: "a
  /// recipient MAY recognize a single LF as a line terminator"). Docker.DotNet's
  /// HTTP client uses bare LFs between headers, so we have to split on `\n`
  /// and trim trailing `\r` from each line, then re-emit canonical CRLF.
  /// </summary>
  internal static byte[] RewriteRequestForceClose(byte[] buf, int totalRead, int headersEnd, int bodyStart, byte[]? bodyOverride = null)
  {
    // headersEnd points at the first byte of the empty-line terminator.
    // bodyStart is headersEnd + terminatorLength (passed in from the caller).
    var headerSection = Encoding.ASCII.GetString(buf, 0, headersEnd);

    // Split on \n then trim trailing \r — handles \r\n, bare \n, and mixed.
    var lines = headerSection.Split('\n');

    var rebuilt = new StringBuilder(headerSection.Length + 64);
    rebuilt.Append(lines[0].TrimEnd('\r')).Append("\r\n");

    // When a body override is supplied (Phase 2 inspection rewrote the JSON)
    // we also strip any existing Content-Length / Transfer-Encoding so the
    // upstream daemon reads exactly the bytes we hand it. We append a fresh
    // Content-Length below.
    var hasBodyOverride = bodyOverride is not null;

    for (int i = 1; i < lines.Length; i++)
    {
      var line = lines[i].TrimEnd('\r');
      if (line.Length == 0) continue;

      // Strip any header that controls connection persistence — we override it.
      if (line.StartsWith("Connection:", StringComparison.OrdinalIgnoreCase)) continue;
      if (line.StartsWith("Proxy-Connection:", StringComparison.OrdinalIgnoreCase)) continue;
      if (line.StartsWith("Keep-Alive:", StringComparison.OrdinalIgnoreCase)) continue;

      if (hasBodyOverride)
      {
        if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) continue;
        if (line.StartsWith("Transfer-Encoding:", StringComparison.OrdinalIgnoreCase)) continue;
      }

      rebuilt.Append(line).Append("\r\n");
    }

    rebuilt.Append("Connection: close\r\n");
    if (hasBodyOverride)
    {
      rebuilt.Append("Content-Length: ").Append(bodyOverride!.Length).Append("\r\n");
    }
    rebuilt.Append("\r\n");

    var newHeaderBytes = Encoding.ASCII.GetBytes(rebuilt.ToString());

    if (hasBodyOverride)
    {
      var combinedOverride = new byte[newHeaderBytes.Length + bodyOverride!.Length];
      Array.Copy(newHeaderBytes, 0, combinedOverride, 0, newHeaderBytes.Length);
      Array.Copy(bodyOverride, 0, combinedOverride, newHeaderBytes.Length, bodyOverride.Length);
      return combinedOverride;
    }

    var bodyLen = Math.Max(0, totalRead - bodyStart);

    var combined = new byte[newHeaderBytes.Length + bodyLen];
    Array.Copy(newHeaderBytes, 0, combined, 0, newHeaderBytes.Length);
    if (bodyLen > 0)
    {
      Array.Copy(buf, bodyStart, combined, newHeaderBytes.Length, bodyLen);
    }
    return combined;
  }

  /// <summary>
  /// Returns true if the buffered request looks like an HTTP Upgrade / hijacking
  /// request — Docker uses this for `exec`, `attach`, and `logs --follow` style
  /// endpoints that need bidirectional streaming after the response. We can't
  /// rewrite these to Connection: close because the upgrade requires keeping
  /// the socket open.
  /// </summary>
  internal static bool LooksLikeHijack(byte[] buf, int headersEnd)
  {
    var headerSection = Encoding.ASCII.GetString(buf, 0, headersEnd);
    // Split on bare \n then trim trailing \r — same approach as
    // RewriteRequestForceClose. The previous strict "\r\n" split missed
    // bare-LF terminators emitted by clients like Docker.DotNet's
    // ManagedHandler, which would cause LooksLikeHijack to return false
    // for a real Upgrade request, the broker to apply Connection: close,
    // and exec/attach to break with the same class of bug as the original
    // header-end-detection issue.
    foreach (var rawLine in headerSection.Split('\n'))
    {
      var line = rawLine.TrimEnd('\r');
      if (line.StartsWith("Upgrade:", StringComparison.OrdinalIgnoreCase)) return true;
      if (line.StartsWith("Connection:", StringComparison.OrdinalIgnoreCase) &&
          line.Contains("upgrade", StringComparison.OrdinalIgnoreCase))
      {
        return true;
      }
    }
    return false;
  }

  // ─── Body inspection ────────────────────────────────────────────────────────

  private readonly record struct CreateBodyOutcome(
    bool Blocked,
    string Reason,
    byte[]? RewrittenBody,
    bool FullyConsumed);

  /// <summary>
  /// Reads the full request body for POST /containers/create and runs it through
  /// <see cref="DockerBrokerBodyInspector"/>. Returns:
  ///   * Blocked: true → caller emits 403 and closes the connection.
  ///   * RewrittenBody: non-null → caller forwards a request with this body and
  ///     a recomputed Content-Length.
  ///   * FullyConsumed: true → the client has nothing more to write, so the
  ///     splice loop must skip the client→upstream half (otherwise it blocks
  ///     on a socket the caller has already drained).
  ///
  /// Falls back to "allow without rewrite" when:
  ///   * Content-Length is missing or unparseable
  ///   * the body uses chunked transfer encoding (we don't dechunk yet)
  ///   * the body is larger than <see cref="DockerBrokerBodyInspector.MaxInspectableBodyBytes"/>
  ///
  /// In each fallback case the request flows through unchanged. The endpoint
  /// allowlist already gates which paths can be reached at all, so this is a
  /// degraded-not-bypassed posture.
  /// </summary>
  private async Task<CreateBodyOutcome> ReadAndInspectCreateBodyAsync(
    Stream clientStream,
    byte[] headerBuf,
    int totalRead,
    int headersEnd,
    int bodyStart,
    CancellationToken ct)
  {
    var headerSection = Encoding.ASCII.GetString(headerBuf, 0, headersEnd);

    // Bail on chunked — dechunking is complex enough to defer to a follow-up.
    if (HeaderContains(headerSection, "Transfer-Encoding:", "chunked"))
    {
      DebugLogger.Log("DockerSocketBroker: skipping body inspection for chunked POST /containers/create");
      return new CreateBodyOutcome(false, "skipped (chunked body)", null, false);
    }

    var contentLength = ParseContentLength(headerSection);
    if (contentLength is null)
    {
      DebugLogger.Log("DockerSocketBroker: skipping body inspection — no Content-Length");
      return new CreateBodyOutcome(false, "skipped (no Content-Length)", null, false);
    }

    if (contentLength.Value > DockerBrokerBodyInspector.MaxInspectableBodyBytes)
    {
      DebugLogger.Log($"DockerSocketBroker: skipping body inspection — body {contentLength.Value} exceeds inspection limit");
      return new CreateBodyOutcome(false, "skipped (body too large)", null, false);
    }

    var totalBodyBytes = contentLength.Value;
    var bodyBuffer = new byte[totalBodyBytes];

    // Copy whatever the initial read already pulled past the header terminator.
    var alreadyBuffered = Math.Max(0, totalRead - bodyStart);
    if (alreadyBuffered > totalBodyBytes)
    {
      // The buffered region claims to extend past Content-Length. Trust the
      // header and only copy the declared length — anything else would be a
      // protocol violation we can't safely forward.
      alreadyBuffered = totalBodyBytes;
    }
    if (alreadyBuffered > 0)
    {
      Array.Copy(headerBuf, bodyStart, bodyBuffer, 0, alreadyBuffered);
    }

    var remaining = totalBodyBytes - alreadyBuffered;
    var offset = alreadyBuffered;
    while (remaining > 0)
    {
      var n = await clientStream.ReadAsync(bodyBuffer.AsMemory(offset, remaining), ct).ConfigureAwait(false);
      if (n <= 0)
      {
        // Client closed mid-body. Treat as a malformed request — block.
        return new CreateBodyOutcome(true, "client closed during body read", null, true);
      }
      offset += n;
      remaining -= n;
    }

    var result = DockerBrokerBodyInspector.Inspect(bodyBuffer, SiblingNetworkName, _config.BodyInspection);
    if (!result.Allowed)
    {
      return new CreateBodyOutcome(true, result.Reason, null, true);
    }

    // Whether or not we mutated the body, we've now consumed every byte the
    // client intended to send. The splice loop must skip the client→upstream
    // half so it doesn't hang waiting for bytes that won't arrive.
    return new CreateBodyOutcome(false, result.Reason, result.RewrittenBody ?? bodyBuffer, true);
  }

  private static bool HeaderContains(string headerSection, string headerName, string value)
  {
    foreach (var rawLine in headerSection.Split('\n'))
    {
      var line = rawLine.TrimEnd('\r');
      if (line.StartsWith(headerName, StringComparison.OrdinalIgnoreCase) &&
          line.Contains(value, StringComparison.OrdinalIgnoreCase))
      {
        return true;
      }
    }
    return false;
  }

  private static int? ParseContentLength(string headerSection)
  {
    foreach (var rawLine in headerSection.Split('\n'))
    {
      var line = rawLine.TrimEnd('\r');
      if (!line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) continue;
      var value = line["Content-Length:".Length..].Trim();
      if (int.TryParse(value, out var parsed) && parsed >= 0) return parsed;
      return null;
    }
    return null;
  }

  // ─── Helpers ────────────────────────────────────────────────────────────────

  /// <summary>
  /// Locates the end of the HTTP header block. The empty line separating headers
  /// from body can be expressed four ways depending on whether the sender uses
  /// CRLF or bare LF terminators (RFC 7230 §3.5 — recipients SHOULD accept both):
  /// <list type="bullet">
  ///   <item>"\r\n\r\n" (4 bytes) — strict CRLF</item>
  ///   <item>"\n\n" (2 bytes) — bare LF throughout</item>
  ///   <item>"\r\n\n" or "\n\r\n" (3 bytes) — mixed</item>
  /// </list>
  /// Returns the index of the FIRST byte of the terminator and the terminator
  /// length via the out parameter, or -1 if no terminator is in the buffer yet.
  ///
  /// We have to handle bare LF because Docker.DotNet's <c>Microsoft.Net.Http.Client</c>
  /// emits headers with `\n` terminators between headers and only a final `\r\n`,
  /// producing the `\n...\n\r\n` shape — which our previous strict CRLF check
  /// missed entirely, leaving the broker waiting for a terminator that never came.
  /// </summary>
  internal static int IndexOfHeadersEnd(byte[] buf, int length, out int terminatorLength)
  {
    for (int i = 0; i < length; i++)
    {
      // 4-byte: \r\n\r\n
      if (i + 3 < length && buf[i] == '\r' && buf[i + 1] == '\n' && buf[i + 2] == '\r' && buf[i + 3] == '\n')
      {
        terminatorLength = 4;
        return i;
      }
      // 3-byte: \r\n\n
      if (i + 2 < length && buf[i] == '\r' && buf[i + 1] == '\n' && buf[i + 2] == '\n')
      {
        terminatorLength = 3;
        return i;
      }
      // 3-byte: \n\r\n
      if (i + 2 < length && buf[i] == '\n' && buf[i + 1] == '\r' && buf[i + 2] == '\n')
      {
        terminatorLength = 3;
        return i;
      }
      // 2-byte: \n\n
      if (i + 1 < length && buf[i] == '\n' && buf[i + 1] == '\n')
      {
        terminatorLength = 2;
        return i;
      }
    }
    terminatorLength = 0;
    return -1;
  }

  /// <summary>
  /// Returns the index of the first line terminator (CRLF or bare LF) in the
  /// buffer, used to find the end of the request line.
  /// </summary>
  internal static int IndexOfLineTerminator(byte[] buf, int length)
  {
    for (int i = 0; i < length; i++)
    {
      if (buf[i] == '\r' && i + 1 < length && buf[i + 1] == '\n') return i;
      if (buf[i] == '\n') return i;
    }
    return -1;
  }

  private static async Task WriteSimpleResponseAsync(Stream stream, int status, string reason, CancellationToken ct)
  {
    var body = reason;
    var bytes = Encoding.ASCII.GetBytes(
      $"HTTP/1.1 {status} {GetReasonPhrase(status)}\r\n" +
      $"Content-Type: text/plain\r\n" +
      $"Content-Length: {body.Length}\r\n" +
      $"Connection: close\r\n\r\n{body}");
    await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
  }

  private static async Task WriteJsonErrorAsync(Stream stream, int status, string message, CancellationToken ct)
  {
    var body = $"{{\"message\":\"{EscapeJson(message)}\"}}";
    var bytes = Encoding.ASCII.GetBytes(
      $"HTTP/1.1 {status} {GetReasonPhrase(status)}\r\n" +
      $"Content-Type: application/json\r\n" +
      $"Content-Length: {body.Length}\r\n" +
      $"Connection: close\r\n\r\n{body}");
    await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
  }

  private static string GetReasonPhrase(int status) => status switch
  {
    400 => "Bad Request",
    403 => "Forbidden",
    431 => "Request Header Fields Too Large",
    502 => "Bad Gateway",
    _ => "Error"
  };

  // ─── Cleanup ────────────────────────────────────────────────────────────────

  public async ValueTask DisposeAsync()
  {
    if (_disposed) return;
    _disposed = true;

    try { _cts?.Cancel(); } catch { /* ignore */ }

    try
    {
      _unixListener?.Close();
      _tcpListener?.Stop();
    }
    catch
    {
      // Closing the listener races with the accept loop; we don't care.
    }

    if (_acceptLoop is not null)
    {
      try
      {
        await _acceptLoop.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
      }
      catch
      {
        // Best-effort shutdown.
      }
    }

    if (_listen.IsUnix)
    {
      try { if (File.Exists(_listen.UnixPath!)) File.Delete(_listen.UnixPath!); } catch { /* ignore */ }
    }

    _cts?.Dispose();
  }

  /// <summary>
  /// Sweeps stale broker socket files from /tmp older than 1 hour. Called at the
  /// start of each --dind session so a kill -9'd previous run doesn't leave
  /// debris behind.
  /// </summary>
  public static void CleanupOrphanedSockets()
  {
    try
    {
      var dir = Path.GetTempPath();
      var cutoff = DateTime.UtcNow - TimeSpan.FromHours(1);
      foreach (var file in Directory.EnumerateFiles(dir, "copilot-broker-*.sock"))
      {
        try
        {
          if (File.GetLastWriteTimeUtc(file) < cutoff)
          {
            File.Delete(file);
          }
        }
        catch
        {
          // Ignore individual file failures.
        }
      }
    }
    catch
    {
      // Sweeping is best-effort.
    }
  }
}
