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

      // Buffer the request line + headers (everything up to \r\n\r\n).
      // Cap at 64 KiB — Docker API request headers are rarely anywhere near that size.
      const int MaxHeaderBytes = 65536;
      var headerBuf = new byte[MaxHeaderBytes];
      int totalRead = 0;
      int headersEnd = -1;

      while (totalRead < headerBuf.Length)
      {
        var n = await clientStream.ReadAsync(headerBuf.AsMemory(totalRead, headerBuf.Length - totalRead), ct).ConfigureAwait(false);
        if (n <= 0) return;
        totalRead += n;
        headersEnd = IndexOfDoubleCrLf(headerBuf, totalRead);
        if (headersEnd >= 0) break;
      }

      if (headersEnd < 0)
      {
        await WriteSimpleResponseAsync(clientStream, 431, "request headers too large", ct).ConfigureAwait(false);
        return;
      }

      // Parse the request line.
      var firstLineEnd = IndexOfCrLf(headerBuf, totalRead);
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

      // CRITICAL: Rewrite the request to force `Connection: close` before forwarding.
      //
      // Why: HTTP/1.1 keep-alive lets a single TCP connection carry many requests.
      // If we just spliced bytes after approving the first request, every subsequent
      // request on the same connection (e.g. POST /containers/create after a benign
      // GET /_ping) would bypass the rule engine. That would defeat the entire point
      // of the broker.
      //
      // By stripping the client's Connection/Keep-Alive headers and inserting
      // `Connection: close`, we tell the upstream daemon to close the socket after
      // the response. The client then has to open a fresh connection for its next
      // request, and that connection goes through CheckRule from scratch.
      //
      // This costs one TCP handshake per Docker API call, which is unmeasurable
      // against a local Unix socket and entirely worth the security guarantee.
      var rewrittenRequest = RewriteRequestForceClose(headerBuf, totalRead, headersEnd);
      await upstream.WriteAsync(rewrittenRequest, ct).ConfigureAwait(false);

      // Splice the response back to the client. The upstream will close after
      // sending its response (because we asked for Connection: close), so the
      // upstream→client copy will hit EOF and unblock here. We don't need to
      // forward more from client→upstream because the request is already
      // delivered in full above (the only reason to keep client→upstream open
      // would be Connection: Upgrade hijacking, see below).
      var isUpgrade = LooksLikeHijack(headerBuf, headersEnd);
      if (isUpgrade)
      {
        // Hijacked endpoints (exec/attach) need bidirectional streaming after
        // the upgrade. For these we have to splice both directions and tolerate
        // the keep-alive bypass — the connection is consumed by a single
        // hijacked stream and is closed by the client when it's done.
        //
        // Note that we did NOT rewrite Connection: close for hijacked requests
        // (we still rewrite for non-hijacked, even on the rare occasion of a
        // pipelined hijack — that's why we re-check the buffered request here).
        var clientToUpstream = clientStream.CopyToAsync(upstream, ct);
        var upstreamToClient = upstream.CopyToAsync(clientStream, ct);
        await Task.WhenAny(clientToUpstream, upstreamToClient).ConfigureAwait(false);
      }
      else
      {
        // Standard request/response: pipe response back, then close.
        await upstream.CopyToAsync(clientStream, ct).ConfigureAwait(false);
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
  /// Segment-aware path matcher. '*' matches a single segment. The pattern and
  /// path must have the same segment count to match — there is no '**' yet.
  /// </summary>
  internal static bool PathMatches(string pattern, string path)
  {
    if (string.IsNullOrEmpty(pattern) || pattern[0] != '/') return false;
    if (string.IsNullOrEmpty(path) || path[0] != '/') return false;

    var patternSegments = pattern[1..].Split('/');
    var pathSegments = path[1..].Split('/');

    if (patternSegments.Length != pathSegments.Length) return false;

    for (int i = 0; i < patternSegments.Length; i++)
    {
      if (patternSegments[i] == "*") continue;
      if (!patternSegments[i].Equals(pathSegments[i], StringComparison.Ordinal)) return false;
    }

    return true;
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
  /// </summary>
  internal static byte[] RewriteRequestForceClose(byte[] buf, int totalRead, int headersEnd)
  {
    // headersEnd points at the first byte of the \r\n\r\n that terminates the headers.
    var headerSection = Encoding.ASCII.GetString(buf, 0, headersEnd);

    // Split on \r\n. lines[0] is the request line; the rest are header lines.
    // The header section ends just before \r\n\r\n so the trailing empty entry
    // is not included.
    var lines = headerSection.Split("\r\n", StringSplitOptions.None);

    var rebuilt = new StringBuilder(headerSection.Length + 32);
    rebuilt.Append(lines[0]).Append("\r\n");

    for (int i = 1; i < lines.Length; i++)
    {
      var line = lines[i];
      if (line.Length == 0) continue;

      // Strip any header that controls connection persistence — we override it.
      if (line.StartsWith("Connection:", StringComparison.OrdinalIgnoreCase)) continue;
      if (line.StartsWith("Proxy-Connection:", StringComparison.OrdinalIgnoreCase)) continue;
      if (line.StartsWith("Keep-Alive:", StringComparison.OrdinalIgnoreCase)) continue;

      rebuilt.Append(line).Append("\r\n");
    }

    rebuilt.Append("Connection: close\r\n");
    rebuilt.Append("\r\n");

    var newHeaderBytes = Encoding.ASCII.GetBytes(rebuilt.ToString());

    // Body bytes that were already buffered (the part of the buffer past the \r\n\r\n).
    // headersEnd points at the first \r of \r\n\r\n, so the body starts at headersEnd + 4.
    var bodyStart = headersEnd + 4;
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
    foreach (var line in headerSection.Split("\r\n", StringSplitOptions.None))
    {
      if (line.StartsWith("Upgrade:", StringComparison.OrdinalIgnoreCase)) return true;
      if (line.StartsWith("Connection:", StringComparison.OrdinalIgnoreCase) &&
          line.Contains("upgrade", StringComparison.OrdinalIgnoreCase))
      {
        return true;
      }
    }
    return false;
  }

  // ─── Helpers ────────────────────────────────────────────────────────────────

  private static int IndexOfDoubleCrLf(byte[] buf, int length)
  {
    for (int i = 0; i + 3 < length; i++)
    {
      if (buf[i] == '\r' && buf[i + 1] == '\n' && buf[i + 2] == '\r' && buf[i + 3] == '\n')
        return i;
    }
    return -1;
  }

  private static int IndexOfCrLf(byte[] buf, int length)
  {
    for (int i = 0; i + 1 < length; i++)
    {
      if (buf[i] == '\r' && buf[i + 1] == '\n') return i;
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
