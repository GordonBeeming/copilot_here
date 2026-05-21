# Versioning

## Format

`YYYY.MM.DD.N`. Date plus iteration. `N` is the Nth release of that day, so the first release on a given day is `.1`, the second is `.2`, and so on.

## Releasing a new version

Open a PR. Merge it to `main`. That's the whole process.

There is no version to bump and no file to edit. When the merge lands, the `compute-version` job in `.github/workflows/publish.yml` reads today's UTC date, counts the existing `cli-v$DATE.*` releases, picks the next number, and runs `scripts/stamp-version.sh` to write it into the shell scripts before they get packaged. The .NET binary picks up the same version through `-p:CopilotHereVersion=...` on the `dotnet` command line.

## Where the version lives

| Path | Source value | Stamped to |
| --- | --- | --- |
| `copilot_here.sh` (2 lines) | `0.0.0-dev` | real version, in CI only |
| `copilot_here.ps1` (2 lines) | `0.0.0-dev` | real version, in CI only |
| `app/Infrastructure/BuildInfo.cs` | derives at runtime from the assembly | n/a |
| `Directory.Build.props` | falls back to `today.0` if `-p:CopilotHereVersion` isn't passed | overridden in CI |

The `0.0.0-dev` placeholder stays in source forever. Stamping happens on a fresh checkout in CI and is never committed back.

## CI behaviour

- Push to `main`: `N` = `gh release list` count of `cli-v$DATE.*` + 1. Real release.
- Pull request, schedule, manual dispatch from a branch: `N` = `0`. The dev stamp keeps the version-format tests passing without consuming a release slot.

## Local development

`dotnet run --project app -- --version` prints today's date with `.0` because `Directory.Build.props` falls back to that when no version override is passed. To preview what a real stamp would look like, pass it explicitly:

```bash
dotnet publish app/CopilotHere.csproj -c Release -p:CopilotHereVersion=2099.01.02.7 -o /tmp/preview
./scripts/stamp-version.sh 2099.01.02.7   # stamps the shell scripts; revert with `git restore`
```
