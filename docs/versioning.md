# Versioning

## Single Source of Truth

The `VERSION` file in the repository root contains the base version. All other version references are derived from it.

## Format

`YYYY.MM.DD.N` where `.N` is the GitHub Actions run number (e.g., `2026.03.08.42`).

- The **date portion** is manually maintained by developers.
- The **revision** is the GitHub Actions `run_number`, which auto-increments on every workflow run, ensuring every build has a unique version.

## How It Works

### VERSION file
Contains a single line with the base version (e.g., `2026.03.08`).

### scripts/stamp-version.sh
Takes a version argument and stamps it into all locations that contain version strings:
- `copilot_here.sh` (comment + variable)
- `copilot_here.ps1` (comment + variable)
- `app/Infrastructure/BuildInfo.cs` (BuildDate constant)
- `packaging/winget/*.yaml` (PackageVersion)

Files keep real versions in source (not placeholders) so local dev works without stamping.

### scripts/bump-version.sh
Convenience wrapper: updates the VERSION file and runs stamp-version.sh.

```bash
./scripts/bump-version.sh 2026.03.08
```

### Directory.Build.props
Reads the version from the VERSION file at build time. CI can override with `-p:CopilotHereVersion=X.Y.Z.N`.

### CI (publish.yml)
The `compute-version` job runs on every workflow trigger and:
1. Reads the base version from `VERSION`
2. Appends `github.run_number` as the revision
3. Outputs the full version: `YYYY.MM.DD.N` (e.g., `2026.03.08.42`)

The `build-cli` job runs `stamp-version.sh` before building, so all artifacts contain the computed version.

## Releasing a New Version

1. Update the date in `VERSION`
2. Run `./scripts/bump-version.sh YYYY.MM.DD`
3. Commit and push to `main`
4. CI auto-computes the revision and publishes
