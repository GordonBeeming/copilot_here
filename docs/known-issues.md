# Known Issues

This document lists known issues and limitations. Click the issue title for full details, workarounds, and discussion.

| Issue | OS | Shell/Terminal | Fix Planned? |
|-------|----|----|-------------|
| Brokered Docker socket: chunked-body create requests skip inspection | All | - | Yes |
| Brokered Docker socket: Windows host is best-effort | Windows | - | Investigating |

## Brokered Docker socket (beta)

The `--dind` flag enables Testcontainers and sibling-container workflows by routing every Docker API call from the workload container through a host-side broker. The broker is part of the `copilot_here` binary itself, owns the rules, and forwards only requests that match the allowlist. This is meaningfully safer than mounting `/var/run/docker.sock` directly:

- The host process stays in control of every call.
- Dangerous endpoint families are denied by default: `swarm`, `services`, `tasks`, `nodes`, `secrets`, `configs`, `plugins`, `session`, `distribution`, `auth`, `events`.
- `POST /containers/create` is body-inspected: `HostConfig.Privileged=true`, host network/PID/IPC namespaces, forbidden bind mounts (`/`, `/etc`, `/var`, `/var/run/docker.sock`, …), and dangerous Linux capabilities (`SYS_ADMIN`, `SYS_MODULE`, …) are all rejected at request time.
- A strict default-deny image allowlist gates which images may be spawned at all. Empty `body_inspection.allowed_images` means nothing can spawn until the user enumerates trusted patterns (e.g. `mcr.microsoft.com/mssql/server:*`).
- In airlock mode, `HostConfig.NetworkMode` is rewritten to the airlock compose network. Spawned siblings join the same internal-only network as the workload, which reaches them by Docker DNS instead of crossing the airlock boundary.
- Rules live in `.copilot_here/docker-broker.json` (local) or `~/.config/copilot_here/docker-broker.json` (global), with `enforce` and `monitor` modes mirroring airlock.

### Current limitations

- **Chunked-body create requests skip inspection.** Body inspection requires a `Content-Length` header on `POST /containers/create`. If the client uses `Transfer-Encoding: chunked` (rare for known-size JSON, common for streaming uploads), the broker forwards the body without inspection because the dechunker isn't implemented yet. The endpoint allowlist still gates the call. Tracked for a follow-up.
- **Bodies larger than 2 MiB skip inspection.** Same posture: forwarded without rewriting, endpoint allowlist still applies. Container create payloads are typically a few KB so this is a generous safety margin.
- **Path canonicalization for bind mounts is string-level.** `IsForbiddenHostPath` matches an entry against the deny list as exact-or-subpath, but doesn't follow symlinks or normalize `..` segments. A bind like `/tmp/../etc/passwd:/mnt` would slip through. The Docker daemon does its own canonicalization but the broker should mirror it for defense-in-depth. Tracked for a follow-up.
- **Windows host is best-effort.** The broker uses TCP loopback on Windows and connects upstream via Docker Desktop's named pipe (`\\.\pipe\docker_engine`). This works on Docker Desktop with WSL2 in most setups; if your environment routes the daemon differently, set `DOCKER_HOST` explicitly or run from a Linux/macOS host.
- **Podman:** Works via runtime detection. The broker queries `podman info --format '{{.Host.RemoteSocket.Path}}'` and falls back to the conventional rootless and rootful socket paths. If your Podman setup doesn't expose `Host.RemoteSocket.Path`, set `DOCKER_HOST=unix:///path/to/podman.sock`.
- **OrbStack:** Works without configuration. OrbStack exposes the standard `/var/run/docker.sock` on macOS, so the broker connects to it the same way as Docker Desktop.
- **DinD + airlock:** Works end-to-end via the proxy container's socat bridge (the airlock proxy is dual-homed and forwards `proxy:2375` to the host broker on macOS / Windows where the airlock network can't reach `host.docker.internal` directly). Spawned siblings get `NetworkMode` rewritten to the airlock compose network, so the workload reaches them by Docker DNS without ever crossing the airlock boundary.

## Reporting New Issues

Found a new issue? Please report it on our [GitHub Issues](https://github.com/GordonBeeming/copilot_here/issues) page with:

- Operating system and version
- Shell version (e.g., PowerShell 5.1, Bash 5.2, Zsh 5.9)
- Steps to reproduce
- Expected vs actual behavior
- Any error messages or screenshots
