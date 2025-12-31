# GitHub Copilot Instructions

## Project Overview

This is a secure, portable Docker environment for running the GitHub Copilot CLI. It provides sandboxed execution with automatic authentication, token validation, and multiple specialized image variants for different development scenarios.

## Code Repos/Name locations part of this "platform"

- website or copilot here website: `../copilot_here-site/`
- blog or blog content - `../xylem/data/blog/`
- docs or our docs - content from this working directory

You can also use the command `session-info` for more info on mounts for this project

## Script Versioning

**CRITICAL RULE**: ALL VERSION NUMBERS MUST BE IDENTICAL ACROSS ALL FILES. No exceptions.

### Version Format

- **Primary version**: Use current date in format `YYYY.MM.DD` (e.g., `2025.12.02`)
- **Same-day updates**: If the version date already matches today's date, append `.1`, `.2`, `.3`, etc.
  - Example: `2025.12.02` → `2025.12.02.1` → `2025.12.02.2`
- **CRITICAL**: Always increment the version when making changes - this triggers re-download for users

### Where to Update Versions (ALL MUST MATCH)

**EVERY TIME** you modify shell functions, CLI binary code, or any functionality, update ALL FOUR version locations to the SAME version:

1. **Bash/Zsh script**: `copilot_here.sh`

   - Line 2: `# Version: YYYY.MM.DD`
   - Line 8: `COPILOT_HERE_VERSION="YYYY.MM.DD"`

2. **PowerShell script**: `copilot_here.ps1`

   - Line 2: `# Version: YYYY.MM.DD`
   - Line 8: `$script:CopilotHereVersion = "YYYY.MM.DD"`

3. **Build properties**: `Directory.Build.props`

   - Line 4: `<CopilotHereVersion>YYYY.MM.DD</CopilotHereVersion>`

4. **Build info**: `app/Infrastructure/BuildInfo.cs`

   - Line 13: `public const string BuildDate = "YYYY.MM.DD";`

5. **This file**: `.github/copilot-instructions.md`
   - Update "Current version" below

### Verification Checklist

Before committing, verify all 5 locations have the EXACT SAME version:

```bash
# Quick check - all should show the same version
grep "Version: " copilot_here.sh
grep "Version: " copilot_here.ps1
grep "COPILOT_HERE_VERSION=" copilot_here.sh
grep "CopilotHereVersion =" copilot_here.ps1
grep "CopilotHereVersion>" Directory.Build.props
grep "BuildDate = " app/Infrastructure/BuildInfo.cs
```

### When to Update Version

- Any modification to shell function code
- Adding new features or options
- Bug fixes in the scripts or CLI binary
- Changes to the CLI binary code
- **Any commit that affects functionality should increment the version**
- **When in doubt, increment the version**

### Script File Synchronization

**CRITICAL**: The standalone script files (`copilot_here.sh` and `copilot_here.ps1`) are the source of truth.

- The README.md uses `curl` commands to download these files directly from the repository.
- Ensure both scripts are kept in sync regarding functionality and version numbers.
- Both scripts MUST have identical version numbers at all times.

**Current version**: 2025.12.31

## Technology Stack

- **CLI Binary**: .NET 10 Native AOT (self-contained, cross-platform)
- **Shell Wrappers**: Bash/Zsh and PowerShell functions
- **Base OS**: Debian (node:20-slim)
- **Runtime**: Node.js 20
- **CLI Tool**: GitHub Copilot CLI (@github/copilot)
- **Container**: Docker (Multi-arch: AMD64 & ARM64)
- **CI/CD**: GitHub Actions
- **Registry**: GitHub Container Registry (ghcr.io)

## Project Architecture

### Native Binary (`app/`)

The core CLI is a .NET 10 Native AOT application that:

- Validates GitHub authentication and token scopes
- Manages Docker image selection and pulling
- Configures mounts, airlock, and container settings
- Builds and executes Docker Compose configurations
- Handles both safe mode (confirmation required) and YOLO mode (auto-approve)

**Key Components:**

- `Program.cs` - Entry point, argument parsing, command routing
- `Commands/Run/RunCommand.cs` - Main execution logic
- `Infrastructure/GitHubAuth.cs` - Token validation and scope checking
- `Infrastructure/DockerRunner.cs` - Docker process management
- `Infrastructure/AirlockRunner.cs` - Network proxy mode
- `Infrastructure/DebugLogger.cs` - Debug logging infrastructure

### Shell Wrappers

Lightweight functions that:

- Download and install the native binary
- Check for updates
- Handle version management
- Stop running containers before updates
- Provide convenience commands: `copilot_here`, `copilot_yolo`

**Files:**

- `copilot_here.sh` - Bash/Zsh functions
- `copilot_here.ps1` - PowerShell functions

### Development Workflow

- `dev-build.sh` - Local development build script
  - Builds native binary for current platform
  - Stops running containers
  - Copies binary to `~/.local/bin`
  - Updates and sources shell script
  - Optionally builds Docker images

## Docker Image Variants

### Base Image

The standard copilot_here image with:

- Node.js 20
- GitHub Copilot CLI
- Git, curl, gpg, gosu
- User permission management

### Playwright Image

Extends the base image with:

- Playwright (latest version)
- Chromium browser with all dependencies
- System libraries for browser automation

**Use Case**: Web testing, browser automation, checking published web content

### .NET Image

Extends the Playwright image with:

- .NET 8 SDK
- .NET 9 SDK
- .NET 10 SDK

**Use Case**: .NET development, building and testing .NET applications with web testing capabilities

## Airlock Network Proxy

Airlock is a security feature that provides network request monitoring and control:

**How it works:**

- App container runs on an isolated network with NO internet access
- All network requests are routed through a MITM proxy container
- Proxy enforces allow/deny rules based on configuration
- Provides visibility into all network traffic

**Configuration:**

- **Local rules**: `.copilot_here/network.json` (project-specific)
- **Global rules**: `~/.config/copilot_here/network.json` (user-wide)
- **Default rules**: `default-airlock-rules.json` (fallback)

**Use cases:**

- Monitor what external resources Copilot accesses
- Block specific domains or endpoints
- Allow only approved network destinations
- Audit network activity during development

**Technical implementation:**

- Proxy: mitmproxy-based container (`docker/Dockerfile.proxy`)
- Runner: `Infrastructure/AirlockRunner.cs` orchestrates Docker Compose setup
- Certificates: CA cert shared between proxy and app containers
- Logging: Network activity logged to `.copilot_here/logs/`

## Project File Structure Rules

### ⚠️ CRITICAL: All Files Must Be in Project Directory

**NEVER write files outside the project root.** All files, directories, and artifacts must be within the project folder.

#### Allowed Locations:

- Project root: `/work` or current working directory
- Any subdirectories under project root
- Temporary files: Use `tmp/` folder within project (add to .gitignore)

#### Forbidden:

- Writing to home directory (`~/`)
- Writing to system directories (`/tmp`, `/var`, etc.)
- Writing outside project boundaries

#### If You Need Temporary Storage:

1. Create `tmp/` folder in project root
2. Add `tmp/` to `.gitignore`
3. Use it for temporary files
4. Clean up when done

### Files to Never Commit

The following files should NEVER be committed to the repository:

- **`blog.md`** - Personal blog post copy, not part of the repository
  - This file is in `.gitignore`
  - Used for drafting blog posts about the project
  - Keep it local only
  - Can be updated with project information but changes stay on local machine
  - When publishing blog post, copy content to actual blog platform

## File Organization Standards

### Documentation Structure

**All documentation files must be organized in the `/docs` folder**, except for standard GitHub files.

#### Standard GitHub Files (keep in root):

- `README.md` - Project overview and getting started guide
- `LICENSE` - Project license
- `SECURITY.md` - Security policies and vulnerability reporting
- `CODE_OF_CONDUCT.md` - Community guidelines (if exists)
- `CONTRIBUTING.md` - Contribution guidelines (if exists)
- `CHANGELOG.md` - Version history (if exists)

#### Documentation Files (must be in `/docs`):

- Docker image documentation
- Architecture documentation
- Design specifications
- Development guides
- Any other project documentation

### Task Documentation

All task outcomes from Copilot jobs and development tasks must be documented in `/docs/tasks/`.

#### Task File Naming Convention:

- **Format**: `YYYYMMDD-XX-topic.md` (XX is a two-digit order number)
- **Example**: `20250109-01-docker-multi-image-setup.md`, `20250109-02-workflow-update.md`
- **Date Format**: Use ISO 8601 date format (YYYYMMDD)
- **Order**: Two-digit sequence number (01, 02, 03...) to track order of tasks on same day
- **Topic**: Use lowercase with hyphens for multi-word topics

#### Task Screenshots:

- **Location**: `/docs/tasks/images/`
- **For Workflow Changes**: Take before/after screenshots when applicable
- **Naming**: `YYYYMMDD-XX-{description}.png` (matches task file)
- **Examples**:
  - `20250109-01-before-workflow.png`
  - `20250109-01-after-workflow.png`
- **In Task Docs**: Reference images with relative paths: `![Description](./images/20250109-01-before-workflow.png)`

#### Task Documentation Guidelines:

1. **Minor Tasks**: Update existing task files instead of creating new ones
   - If a task is a continuation or update to previous work, append to the existing file
   - Add a new section with updated date header within the file
2. **Major Tasks**: Create new task files for significant features or changes

   - New features or components
   - Major refactoring efforts
   - Significant bug fixes
   - Architecture changes

3. **Task File Content Should Include**:
   - Date and brief description at the top
   - Problem/objective statement
   - Solution approach
   - Changes made (file changes, new dependencies, etc.)
   - Testing performed
   - Any follow-up items or known issues
   - **Use standard markdown checkboxes**: `- [ ]` for unchecked, `- [x]` for checked
   - Avoid using emojis (✅, ✓, ❌) for checkboxes - use proper markdown syntax

## Code Style and Patterns

### Shell Scripts

- **CRITICAL**: Scripts must be compatible with both bash AND zsh
- **NEVER use bash-specific syntax** - this is a recurring source of bugs
- Use bash for shell scripts (include shebang: `#!/bin/bash`)
- Test in both bash and zsh before committing
- Set error handling: `set -e`
- Use meaningful variable names
- Add comments for complex logic
- Handle edge cases (missing variables, etc.)

**Bash/Zsh Compatibility Rules:**

- ✅ Use `eval` for dynamic variable access instead of namerefs
- ✅ Split complex command substitutions into separate steps
- ✅ Use POSIX-compatible syntax where possible
- ✅ Iterate arrays by value: `for item in "${array[@]}"`
- ✅ Use manual index iteration: `i=0; while [ $i -lt ${#array[@]} ]; do ... i=$((i+1)); done`
- ❌ **NEVER use `${!array[@]}`** (bash-specific array key expansion)
- ❌ **NEVER use `${!varname}`** (bash-specific indirect expansion)
- ❌ Avoid `local -n` (bash 4.3+ only, namerefs)
- ❌ Avoid `local -a` in eval (may cause issues)
- ❌ Avoid bash-specific array features not in zsh

### Test Writing Standards

**CRITICAL**: All tests for the native binary must be written in C# using the TUnit framework.

**Testing Philosophy:**

- ✅ **C# Tests Only** - Use `tests/CopilotHere.UnitTests/` for all native binary tests
- ✅ **TUnit Framework** - Uses modern async/await patterns
- ✅ **AOT Compatible** - Tests compile with Native AOT
- ❌ **No Shell Scripts for Testing** - Shell scripts should only be used when absolutely necessary (e.g., shell wrapper function testing)

**Test File Organization:**

- Unit tests: `tests/CopilotHere.UnitTests/[FeatureName]Tests.cs`
- Example: `SandboxFlagsTests.cs`, `ConfigFileTests.cs`, `MountEntryTests.cs`

**Test Pattern:**

```csharp
[Test]
public async Task MethodName_Scenario_ExpectedResult()
{
  // Arrange
  // Act
  // Assert
  await Assert.That(result).IsEqualTo(expected);
}
```

**When Shell Tests Are Acceptable:**

- Testing shell wrapper functions themselves (`copilot_here.sh`, `copilot_here.ps1`)
- Platform-specific shell integration
- Must be clearly documented why C# tests can't be used

**Running Tests:**

```bash
cd tests/CopilotHere.UnitTests
dotnet test
```

### C# / .NET Code

- Use modern C# features (record types, pattern matching, etc.)
- Prefer immutability where possible
- Use nullable reference types
- Follow .NET naming conventions (PascalCase for public members)
- Add XML documentation comments for public APIs
- Use AOT-compatible patterns (avoid reflection, dynamic code generation)

### Configuration Priority

**CRITICAL RULE**: All configuration systems must follow the same priority order.

**Priority Order (highest to lowest):**

1. **CLI Arguments** - User's explicit command-line flags (highest priority)
2. **Local Config** - Project-specific config in `.copilot_here/` (medium priority)
3. **Global Config** - User-wide config in `~/.config/copilot_here/` (low priority)
4. **Default Values** - Hardcoded fallback values (lowest priority)

**Implementation Pattern:**

```csharp
// When collecting config from multiple sources:
var items = new List<Item>();

// Add CLI items first (highest priority)
items.AddRange(cliItems);

// Add local config items
items.AddRange(localConfigItems);

// Add global config items last (lowest priority)
items.AddRange(globalConfigItems);

// Then deduplicate, keeping first occurrence
var deduplicated = RemoveDuplicates(items);
```

**Config Files:**

- `ImageConfig` - Image tag selection: CLI > Local > Global > "latest"
- `MountsConfig` - Mount paths: CLI > Local > Global
- `AirlockConfig` - Network proxy: Local > Global > disabled

**Deduplication:**

- When duplicates exist, keep the first occurrence (respects priority order)
- **SECURITY**: Always prefer read-only over read-write within same priority level
- Normalize paths (remove trailing slashes) before comparing
- Example: If CLI has path:ro and path:rw, keep path:ro for security

**Testing:**

- All config systems must have unit tests verifying priority order
- Tests should verify: no configs, global only, local only, both configs
- See: `ImageConfigTests.cs`, `MountsConfigTests.cs`, `AirlockConfigTests.cs`

### Debug Logging

- Use `DebugLogger.Log()` for debug output
- Only enabled when `COPILOT_HERE_DEBUG=1` or `COPILOT_HERE_DEBUG=true`
- Logs to stderr to not interfere with normal output
- Add debug logs at key decision points and before/after major operations
- Include context (arguments, state, exit codes)

### Dockerfiles

- Use official base images
- Combine RUN commands to reduce layers
- Clean up package lists: `rm -rf /var/lib/apt/lists/*`
- Use multi-stage builds when appropriate
- Set environment variables clearly
- Document ARGs with comments

### GitHub Actions Workflows

- Use latest stable action versions
- Add descriptive step names
- Include comments for complex logic
- Use environment variables for reusable values
- Implement proper error handling
- Cache when possible for performance

### Naming Conventions

- **Files**: kebab-case (e.g., `docker-images.md`)
- **Dockerfiles**: `Dockerfile` or `Dockerfile.variant`
- **Scripts**: kebab-case with `.sh` extension
- **Environment Variables**: UPPER_SNAKE_CASE
- **Docker Tags**: lowercase with hyphens

### File Organization

```
/work/
  ├── .github/
  │   ├── copilot-instructions.md  # Repository instructions
  │   └── workflows/               # GitHub Actions workflows
  ├── app/                         # Native CLI application
  │   ├── Commands/                # Command implementations
  │   │   ├── Run/                 # Main run command
  │   │   ├── Images/              # Image management
  │   │   ├── Mounts/              # Mount configuration
  │   │   └── Airlock/             # Network proxy
  │   ├── Infrastructure/          # Core services
  │   │   ├── AppPaths.cs          # Path resolution
  │   │   ├── GitHubAuth.cs        # Authentication
  │   │   ├── DockerRunner.cs      # Docker management
  │   │   ├── AirlockRunner.cs     # Proxy mode
  │   │   └── DebugLogger.cs       # Debug logging
  │   ├── Program.cs               # Entry point
  │   └── CopilotHere.csproj       # Project file
  ├── docker/                      # Docker image definitions
  │   ├── Dockerfile.base          # Base image
  │   ├── Dockerfile.proxy         # Airlock proxy
  │   ├── variants/                # Single-layer variants
  │   └── compound-variants/       # Multi-layer variants
  ├── docs/                        # All documentation
  │   ├── tasks/                   # Task documentation
  │   │   └── images/              # Task screenshots
  │   └── *.md                     # Other docs
  ├── tests/                       # Integration tests
  │   └── integration/             # Shell-based tests
  ├── copilot_here.sh              # Bash/Zsh wrapper
  ├── copilot_here.ps1             # PowerShell wrapper
  ├── dev-build.sh                 # Development build script
  ├── README.md                    # Main documentation
  ├── TROUBLESHOOTING.md           # Debug guide
  └── LICENSE                      # License file
```

## Development Workflow

### Git Workflow - Wait for Approval

**IMPORTANT**: Wait for user approval before committing changes.

### Never Revert User Changes

**CRITICAL RULE**: Never use `git checkout`, `git reset`, or any command that reverts uncommitted changes you didn't make.

**Rules:**

- ❌ **NEVER** revert changes in the working directory that you didn't create
- ❌ **NEVER** use `git checkout <file>` on uncommitted changes
- ❌ **NEVER** use `git reset --hard` or similar commands
- ✅ **ALWAYS** ask the user before reverting any uncommitted changes
- ✅ If you think a change is unrelated, ask the user if they want to keep it

**Why this matters:**

- The user may have intentionally made changes (like configuration files)
- Reverting their work without permission is destructive and disrespectful
- You cannot know the user's intent - always ask first

**Example scenarios:**

- ❌ "I see there's a config change that shouldn't be there. Let me revert that."
- ✅ "I noticed there's a change to `.copilot_here/mounts.conf`. Should we include that in this commit, or would you like to handle it separately?"

### Issue Linking Requirement

**CRITICAL RULE**: ALL commits MUST link to an issue. NO EXCEPTIONS.

**Before committing:**

1. **STOP** - Do you have an issue number?
2. **If NO**: Ask the user "What issue number should I reference for this commit?"
3. **If user doesn't have one**: Offer to create a PBI in `pbi.md` that they can turn into a GitHub issue
4. **ONLY THEN** proceed with commit

**Rules:**

- ❌ **NEVER** commit without an issue reference (e.g., `#123` in commit message)
- ✅ **ALWAYS** ask "What issue number should I reference for this commit?" before committing
- ✅ If no issue exists, create `pbi.md` first for the user to convert to an issue

If the requester doesn't have an issue yet (or details are still fuzzy), offer to draft a **single** backlog entry in `pbi.md` (this file is intentionally ignored by git) so it's quick to turn into a GitHub issue.

**`pbi.md` guidelines (going forward):**

- Write what the issue is (not the resolution). If you have resolution details, put them under a clearly marked section like "Resolution notes (post-issue comment)".
- Focus on the problem and what "working" looks like.
- Keep the structure simple: **Title**, **Summary**, **Acceptance criteria**, **Notes** (add **Environment** / **Steps to reproduce** only when helpful).
- `pbi.md` should contain one PBI at a time; clear/overwrite it each time (don't append multiple PBIs).

#### Commit Guidelines:

1. **Check for issue**: Ask "What issue number should I reference?" - REQUIRED FIRST STEP
2. **Prepare changes**: Make your changes and verify them
3. **Ask for approval**: Present the changes to the user and ask if you should commit
4. **Commit on approval**: Only run the git commit command when the user says "commit" or similar
5. **Descriptive messages**: Use clear, concise commit messages
6. **Fix mistakes**: If you need to fix something in the last commit:
   ```bash
   # Undo last commit but keep changes
   git reset --soft HEAD~1
   # Make your fixes
   git add .
   git commit -m "Fixed: [description]"
   ```

#### When to Ask to Commit:

- ✅ After adding a new feature or component
- ✅ After fixing a bug
- ✅ After updating documentation
- ✅ After refactoring code
- ✅ Before making major changes (safety checkpoint)
- ✅ After successful test runs

#### Commit Message Format:

```
[Type]: Brief description

Examples:
- feat: Add Playwright Docker image variant
- fix: Correct workflow image tagging
- docs: Update Docker images documentation
- refactor: Simplify entrypoint script
- ci: Add multi-image build pipeline
- chore: Update dependencies
```

#### Co-Author Attribution

**ALWAYS add the requester as a co-author on commits** to ensure proper attribution.

**How to identify the requester**:

1. **Git config**: Check `git config user.name` and `git config user.email`
2. **GitHub user**: If running in GitHub Codespaces, use the logged-in GitHub user
3. **GitHub Actions**: When triggered by a comment/issue, use the comment author's details
4. **Manual request**: When someone asks you to make changes, use their information

#### Git Signing

**CRITICAL**: Never modify the user's git signing configuration.

**Rules:**

- ❌ **NEVER** change `git config` signing settings (user.signingkey, commit.gpgsign, etc.)
- ✅ **ALWAYS** use `--no-gpg-sign` flag when committing to bypass signing
- ✅ Leave the user's signing configuration intact for their own commits

**Commit Format** (with co-author and no signing):

```bash
# Use multiple -m flags and --no-gpg-sign
git commit --no-gpg-sign -m "Type: Brief description" \
  -m "Co-authored-by: Name <email@example.com>"
```

**Example**:

```bash
git commit --no-gpg-sign -m "feat: Add Playwright Docker image variant" \
  -m "Co-authored-by: Gordon Beeming <me@gordonbeeming.com>"
```

**Multiple co-authors**:

```bash
git commit --no-gpg-sign -m "feat: Add multi-image build pipeline" \
  -m "Co-authored-by: Gordon Beeming <me@gordonbeeming.com>" \
  -m "Co-authored-by: Other Contributor <other@example.com>"
```

**When to add co-authors**:

- ✅ When implementing a requested feature
- ✅ When fixing a reported bug
- ✅ When making changes based on feedback
- ✅ When pair programming or collaborating
- ❌ Not needed for automated updates (dependency bumps, etc.)
- ❌ Not needed for your own self-initiated refactoring (unless requested)

### Before Making Changes

1. Check existing patterns in the codebase
2. Review documentation in `/docs` for requirements
3. Ensure changes align with project goals
4. Consider impact on both native binary and shell wrappers
5. Check if version numbers need updating

### Making Changes

1. Make minimal, surgical changes - change only what's necessary
2. Follow existing code patterns and conventions
3. Update relevant documentation if making structural changes
4. Test changes locally before committing
5. Update version numbers if changing functionality (see Script Versioning section)

### After Making Changes

1. Test native binary compilation: `dotnet build app/CopilotHere.csproj`
2. Test local build: `./dev-build.sh`
3. Test Docker builds if image changes: `./dev-build.sh --include-all`
4. Verify workflow syntax if modified
5. Run unit tests: `cd tests/CopilotHere.UnitTests && dotnet test`
6. Document significant changes in `/docs/tasks/` following naming conventions
7. **Include screenshots in task docs** with relative image paths if applicable
8. **Review if these instructions need updating**:
   - Did you fix a bug that was caused by missing documentation?
   - Did you add new patterns or conventions that should be documented?
   - Did you discover a common mistake that should be warned against?
   - Update `.github/copilot-instructions.md` to prevent repeating the same mistakes
   - **Add tests for documented patterns** to ensure they are followed
9. **Ask for approval to commit**:
   - **ALWAYS ask before committing** unless explicitly told to auto-commit
   - Explain what changes are ready to be committed
   - Wait for user confirmation ("commit", "yes", "go ahead", etc.)
   - If user says "commit" or similar, proceed with commit
10. **Commit your changes with co-author attribution** (after approval):
    ```bash
    git add . && git commit -m "Type: Description" \
      -m "Co-authored-by: Name <email@example.com>"
    ```
11. **DO NOT push to remote** - Only commit locally, never use `git push`

## Testing and Quality

### Native Binary Testing

Test the CLI application locally:

```bash
# Build and test
cd app
dotnet build
dotnet run -- --help
dotnet run -- --version

# Test with debug logging
COPILOT_HERE_DEBUG=1 dotnet run -- --yolo -p "echo test"
```

### Local Development Build

```bash
# Build binary, update scripts, optionally build images
./dev-build.sh

# Build with Docker images
./dev-build.sh --include-all
```

### Docker Image Testing

Always test Docker images before committing changes.

#### Local Build Testing:

```bash
# Test base image
docker build -t copilot_here:test .

# Test Playwright image
docker build -f Dockerfile.playwright --build-arg BASE_IMAGE_TAG=test -t copilot_here:playwright-test .

# Test .NET image
docker build -f Dockerfile.dotnet --build-arg PLAYWRIGHT_IMAGE_TAG=playwright-test -t copilot_here:dotnet-test .

# Run container to verify
docker run --rm -it copilot_here:test copilot --version
```

### Workflow Testing

- Validate YAML syntax before committing
- Test workflow locally with act (if available)
- Review workflow runs in GitHub Actions
- Check for proper image tagging

### Before Committing

- Ensure native binary builds successfully: `dotnet build app/CopilotHere.csproj`
- Test local dev build: `./dev-build.sh`
- Ensure Dockerfiles build successfully (if modified)
- Verify workflow YAML is valid (if modified)
- Test affected functionality
- Review file changes with `git diff`
- Ensure documentation is updated
- Check version numbers are updated if functionality changed

### Edge Cases to Consider

- Missing environment variables
- Different user permissions
- Various Docker versions
- GitHub Actions platform differences
- Image size optimization
- Build cache behavior

## Build and Deployment

### Native Binary Build Process

The CLI is built as a self-contained .NET Native AOT binary:

- **Platforms**: linux-x64, linux-arm64, osx-x64, osx-arm64, win-x64, win-arm64
- **Output**: Single-file executable (no runtime required)
- **Trimming**: Enabled for smaller binary size
- **AOT**: Native ahead-of-time compilation for fast startup
- **Release**: Published via GitHub Actions on tags matching `cli-*`

### Docker Build Process

The images build in sequence:

1. **Base Image** (Dockerfile) → `latest`, `main`, `sha-<commit>`
2. **Playwright Image** (Dockerfile.playwright) → `playwright`, `playwright-sha-<commit>`
3. **.NET Image** (Dockerfile.dotnet) → `dotnet`, `dotnet-sha-<commit>`

### GitHub Actions Workflow

- **Triggers**: Push to main, nightly schedule, manual dispatch
- **Registry**: ghcr.io (GitHub Container Registry)
- **Authentication**: Automatic via GITHUB_TOKEN
- **Caching**: Uses registry cache for faster builds
- **Conditional Push**: Only pushes when changes detected or on push events

### Image Tags

Each image variant gets multiple tags:

- Latest version tag (e.g., `playwright`, `dotnet`)
- Commit-specific tag (e.g., `playwright-sha-abc123`)
- Base image gets: `latest`, `main`, `sha-<commit>`

## Important Reminders

### ⚠️ Critical Guidelines

1. **Wait for approval** - Ask before committing changes
2. **Fix commits if needed** - Use `git reset --soft HEAD~1` to undo last commit and fix
3. **Add co-authors to commits** - Always attribute the requester (see Git Workflow section)
4. **All files in project directory** - Never write outside project root
5. **Keep these instructions updated** - Especially when adding new features
6. **All docs in `/docs`** - Except standard GitHub files
7. **Task files use date prefix** - `YYYYMMDD-XX-topic.md` format (XX = order number)
8. **Task screenshots in `/docs/tasks/images/`** - When applicable for workflow/UI changes
9. **Minor tasks update existing files** - Don't create duplicate task files
10. **Document major changes** - Create task files for significant work
11. **Test before committing** - Build binary and verify functionality
12. **Update versions** - Increment version numbers when changing functionality
13. **Stop containers before updating** - dev-build.sh will prompt to stop running containers
14. **Debug logging** - Use `COPILOT_HERE_DEBUG=1` to enable detailed logging

### When to Update These Instructions

- Adding new Docker image variants
- Changing build process
- Adding new tools or dependencies
- Establishing new coding patterns
- Adding new development workflows
- Changing documentation structure

---

**Last Updated**: 2025-12-05
**Version**: 2.0.0
**CLI Binary**: .NET 10 Native AOT
**Docker Base**: node:20-slim
**Image Variants**: 8 (base, playwright, dotnet, dotnet-8, dotnet-9, dotnet-10, rust, dotnet-rust)
**Registry**: ghcr.io/gordonbeeming/copilot_here
