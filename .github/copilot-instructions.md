# GitHub Copilot Instructions

## Project Overview
This is a secure, portable Docker environment for running the GitHub Copilot CLI. It provides sandboxed execution with automatic authentication, token validation, and multiple specialized image variants for different development scenarios.

## Script Versioning

**IMPORTANT**: Shell function scripts (Bash/Zsh and PowerShell) in the README must have version headers updated when modified.

### Version Format
- **Primary version**: Use current date in format `YYYY-MM-DD` (e.g., `2025-10-27`)
- **Same-day updates**: If the version date already matches today's date, append `.1`, `.2`, `.3`, etc.
  - Example: `2025-10-27` → `2025-10-27.1` → `2025-10-27.2`

### Where to Update Versions
When modifying shell functions in README.md, update ALL version references:
1. Bash/Zsh script header comment: `# Version: YYYY-MM-DD`
2. PowerShell script header comment: `# Version: YYYY-MM-DD`
3. Bash copilot_here help text: `VERSION: YYYY-MM-DD`
4. Bash copilot_yolo help text: `VERSION: YYYY-MM-DD`
5. PowerShell Copilot-Here help text: `VERSION: YYYY-MM-DD`
6. PowerShell Copilot-Yolo help text: `VERSION: YYYY-MM-DD`

### When to Update Version
- Any modification to shell function code
- Adding new features or options
- Bug fixes in the scripts
- Changes to help text or documentation within scripts

### Script File Synchronization
**CRITICAL**: When updating the shell scripts, you must update BOTH locations:
1. **Standalone script files**: `copilot_here.sh` and `copilot_here.ps1` (auto-install method)
2. **README manual installation sections**: The code blocks users copy/paste for manual setup

After making changes to README:
```bash
# Regenerate standalone files from README
awk '/^   ```bash$/,/^   ```$/ {if (!/^   ```/) print}' README.md | sed 's/^   //' > copilot_here.sh
awk '/^   ```powershell$/,/^   ```$/ {if (!/^   ```/) print}' README.md | sed 's/^   //' > copilot_here.ps1
```

Both locations must stay in sync so auto-install and manual install get the same functionality.

**Current version**: 2025-11-05.1

## Technology Stack
- **Base OS**: Debian (node:20-slim)
- **Runtime**: Node.js 20
- **CLI Tool**: GitHub Copilot CLI (@github/copilot)
- **Container**: Docker
- **CI/CD**: GitHub Actions
- **Registry**: GitHub Container Registry (ghcr.io)

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

**Use Case**: .NET development, building and testing .NET applications with web testing capabilities

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
**CRITICAL**: Every test must have versions for ALL supported shells unless platform-specific.

**Required Test Versions:**
- ✅ **Bash** - `tests/integration/test_*.sh` (using `#!/bin/bash`)
- ✅ **Zsh** - `tests/integration/test_*_zsh.sh` (using `#!/bin/zsh`) OR separate test suite
- ✅ **PowerShell** - `tests/integration/test_*.ps1`

**Test Consistency Across Platforms:**
- ⚠️ **CRITICAL**: When modifying test logic in one shell version, you MUST update ALL shell versions with equivalent changes
- ✅ If you add a test case to Bash tests, add it to Zsh and PowerShell tests
- ✅ If you fix a bug in PowerShell tests, check if the same fix is needed in Bash/Zsh tests
- ✅ If you update path handling in one test, update all test files with the same logic
- ❌ **NEVER** change logic in only one test file and leave others outdated
- ❌ **NEVER** add features to one platform's tests without considering the others

**When a test is platform-specific:**
- Add a clear comment at the top explaining why
- Example: `# Linux/macOS only - tests symbolic link following with readlink`
- Example: `# Windows only - tests Windows path handling with backslashes`

**Test File Naming:**
- Bash: `test_bash.sh`, `test_docker_commands.sh`, `test_mount_config.sh`
- Zsh: `test_zsh.sh`, `test_docker_commands_zsh.sh`
- PowerShell: `test_powershell.ps1`

**Test Coverage Requirements:**
- Each test file should validate the same functionality across platforms
- Test counts may vary (e.g., Zsh arrays are 1-indexed vs 0-indexed in Bash)
- Document any shell-specific behavior differences in comments
- All tests must pass in their respective environments
- When creating test data (paths, files), ensure they exist before validation to avoid warnings

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
  │   └── workflows/     # GitHub Actions workflows
  ├── docs/              # All documentation
  │   ├── tasks/         # Task documentation
  │   │   └── images/    # Task screenshots
  │   └── *.md           # Other docs
  ├── Dockerfile         # Base image
  ├── Dockerfile.*       # Image variants
  ├── *.sh               # Shell scripts
  ├── README.md          # Main documentation
  ├── LICENSE            # License file
  └── .gitignore         # Git ignore rules
```

## Development Workflow

### Git Workflow - Commit as You Go
**IMPORTANT**: Commit changes incrementally as you complete logical units of work.

#### Commit Guidelines:
1. **Commit frequently**: After completing each logical change or fix
2. **Small, focused commits**: Each commit should represent one change
3. **Descriptive messages**: Use clear, concise commit messages
4. **Fix mistakes**: If you need to fix something in the last commit:
   ```bash
   # Undo last commit but keep changes
   git reset --soft HEAD~1
   # Make your fixes
   git add .
   git commit -m "Fixed: [description]"
   ```

#### When to Commit:
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

**Co-Author Format**:
```bash
git commit -m "Type: Brief description

Co-authored-by: Name <email@example.com>"
```

**Example**:
```bash
git commit -m "feat: Add Playwright Docker image variant

Co-authored-by: Gordon Beeming <me@gordonbeeming.com>"
```

**Multiple co-authors**:
```bash
git commit -m "feat: Add multi-image build pipeline

Co-authored-by: Gordon Beeming <me@gordonbeeming.com>
Co-authored-by: Other Contributor <other@example.com>"
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

### Making Changes
1. Make minimal, surgical changes - change only what's necessary
2. Follow existing code patterns and conventions
3. Update relevant documentation if making structural changes
4. Test changes locally before committing

### After Making Changes
1. Test Docker builds locally: `docker build -t test .`
2. Verify workflow syntax if modified
3. Document significant changes in `/docs/tasks/` following naming conventions
4. **Include screenshots in task docs** with relative image paths if applicable
5. **Commit your changes with co-author attribution**:
   ```bash
   git add . && git commit -m "Type: Description

   Co-authored-by: Name <email@example.com>"
   ```
6. **DO NOT push to remote** - Only commit locally, never use `git push`

## Testing and Quality

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
- Ensure Dockerfiles build successfully
- Verify workflow YAML is valid
- Test affected functionality
- Review file changes with `git diff`
- Ensure documentation is updated

### Edge Cases to Consider
- Missing environment variables
- Different user permissions
- Various Docker versions
- GitHub Actions platform differences
- Image size optimization
- Build cache behavior

## Build and Deployment

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
1. **Commit as you go** - Make incremental commits after each logical change
2. **Fix commits if needed** - Use `git reset --soft HEAD~1` to undo last commit and fix
3. **Add co-authors to commits** - Always attribute the requester (see Git Workflow section)
4. **All files in project directory** - Never write outside project root
5. **Keep these instructions updated** - Especially when adding new features
6. **All docs in `/docs`** - Except standard GitHub files
7. **Task files use date prefix** - `YYYYMMDD-XX-topic.md` format (XX = order number)
8. **Task screenshots in `/docs/tasks/images/`** - When applicable for workflow/UI changes
9. **Minor tasks update existing files** - Don't create duplicate task files
10. **Document major changes** - Create task files for significant work
11. **Test before committing** - Build Docker images and verify functionality

### When to Update These Instructions
- Adding new Docker image variants
- Changing build process
- Adding new tools or dependencies
- Establishing new coding patterns
- Adding new development workflows
- Changing documentation structure

---

**Last Updated**: 2025-01-09
**Version**: 1.0.0
**Docker Base**: node:20-slim
**Image Variants**: 3 (base, playwright, dotnet)
**Registry**: ghcr.io/gordonbeeming/copilot_here
