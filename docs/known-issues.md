# Known Issues

This document lists known issues and limitations. Click the issue title for full details, workarounds, and discussion.

| Issue | OS | Shell/Terminal | Fix Planned? |
|-------|----|----|-------------|
| Brokered Docker socket: no body inspection in Phase 1 | All | - | Yes (next phase) |
| Brokered Docker socket: Windows host is best-effort | Windows | - | Investigating |

## Brokered Docker socket (beta)

The `--dind` flag enables Testcontainers and sibling-container workflows by routing every Docker API call from the workload container through a host-side broker. The broker is part of the `copilot_here` binary itself, owns the rules, and forwards only requests that match the allowlist. This is meaningfully safer than mounting `/var/run/docker.sock` directly:

- The host process stays in control of every call.
- Dangerous endpoint families are denied by default: `swarm`, `services`, `tasks`, `nodes`, `secrets`, `configs`, `plugins`, `session`, `distribution`, `auth`, `events`.
- Rules live in `.copilot_here/docker-broker.json` (local) or `~/.config/copilot_here/docker-broker.json` (global), with `enforce` and `monitor` modes mirroring airlock.

### Phase 1 limitations

- **No request body inspection.** The broker filters by HTTP method and URL path only. A motivated AI agent inside the container could `POST /containers/create` with `Privileged: true` or `Binds: ["/:/host"]` and the broker would forward the request. Phase 2 will reject these at the request body level. Tracked separately.
- **Windows host is best-effort.** The broker uses TCP loopback on Windows and connects upstream via Docker Desktop's named pipe (`\\.\pipe\docker_engine`). This works on Docker Desktop with WSL2 in most setups; if your environment routes the daemon differently, set `DOCKER_HOST` explicitly or run from a Linux/macOS host.
- **Podman:** Works via runtime detection. The broker queries `podman info --format '{{.Host.RemoteSocket.Path}}'` and falls back to the conventional rootless and rootful socket paths. If your Podman setup doesn't expose `Host.RemoteSocket.Path`, set `DOCKER_HOST=unix:///path/to/podman.sock`.
- **OrbStack:** Works without configuration. OrbStack exposes the standard `/var/run/docker.sock` on macOS, so the broker connects to it the same way as Docker Desktop.
- **DinD + airlock:** The combination is allowed but emits a warning at startup. Containers spawned by the AI bypass the airlock HTTP proxy because they connect to the host daemon directly. Only the AI agent's own outbound traffic stays inside the airlock.

## Reporting New Issues

Found a new issue? Please report it on our [GitHub Issues](https://github.com/GordonBeeming/copilot_here/issues) page with:

- Operating system and version
- Shell version (e.g., PowerShell 5.1, Bash 5.2, Zsh 5.9)
- Steps to reproduce
- Expected vs actual behavior
- Any error messages or screenshots
