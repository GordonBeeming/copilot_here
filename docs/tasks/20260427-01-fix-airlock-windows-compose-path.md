# Fix airlock path format in compose YAML on Windows

**Date:** 2026-04-27
**Issue:** [#105 - Airlock mode fails in setting up network due to permission error](https://github.com/GordonBeeming/copilot_here/issues/105)

## Problem

Running airlock on Windows produced this error from the Docker daemon as soon as the proxy container started:

```
Container testapp-...-proxy Error response from daemon:
make cli opts(): making volume mountpoint for volume
/c/Users/[USERNAME]/.config/copilot_here/tmp/network-...json: mkdir /c: permission denied
```

The temp file existed on disk. The daemon was looking for it at the wrong location.

## Root cause

PR #45 added `ConvertToDockerPath` to translate `C:\foo` into `/c/foo`. That fix was correct for `docker run -v` because PowerShell 5.1 can't quote a drive-letter path with three colons (`C:\foo:/bar:rw`) cleanly, and the Docker CLI accepts the `/c/foo` form.

The same helper was reused for paths embedded in the airlock `docker-compose.yml`. Compose hands the volume source string straight to the daemon, which evaluates it inside the Linux VM. There, `/c/Users/...` is a literal Linux absolute path. The daemon can't find it, tries to create the mountpoint, and trips over permissions on `/c`.

Five sites in `app/Infrastructure/AirlockRunner.cs` were affected: `{{NETWORK_CONFIG}}`, `{{WORK_DIR}}`, `{{TOOL_CONFIG}}`, the extra-mounts loop, and the optional logs mount.

## Fix

Added `AirlockRunner.ConvertToComposePath`, which only replaces backslashes with forward slashes and leaves the drive letter intact (`C:\foo` → `C:/foo`). Docker Desktop on Windows accepts native drive paths in compose YAML. Switched the five sites to the new helper.

The `docker run -v` path in `RunCommand.cs` and `_MountsConfig.cs` is unchanged — `ConvertToDockerPath` still produces `/c/foo` there because that's what the CLI needs.

Audited every host-path → container-path site to confirm nothing else feeds compose YAML. `SessionInfo.cs` writes raw paths into JSON metadata, which is fine.

## Files changed

- `app/Infrastructure/AirlockRunner.cs` — replaced `ConvertToDockerPath` with `ConvertToComposePath`, swapped five call sites
- `tests/CopilotHere.UnitTests/AirlockComposePathTests.cs` — six regression tests covering Windows drive letters, already-normalised input, Unix paths, and an explicit guard against the `/c/` form returning

## Verification

- `dotnet build app/CopilotHere.csproj` — clean
- `dotnet test --project tests/CopilotHere.UnitTests/CopilotHere.UnitTests.csproj` — 562 passed, including the six new ones
- macOS smoke test — compose YAML on Unix is unchanged because `Replace("\\", "/")` is a no-op on a path that already uses forward slashes
- Awaiting confirmation on the original Windows + Docker Desktop setup from #105
