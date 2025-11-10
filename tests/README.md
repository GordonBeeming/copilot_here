# Integration Tests

This directory contains integration tests for the copilot_here shell scripts.

## Test Files

- **test_bash.sh** - Tests for Bash shell compatibility
- **test_zsh.sh** - Tests for Zsh shell compatibility  
- **test_powershell.ps1** - Tests for PowerShell compatibility

## Running Tests Locally

### Run All Tests
```bash
./tests/run_all_tests.sh
```

### Run Individual Test Suites

**Bash:**
```bash
bash tests/integration/test_bash.sh
```

**Zsh:**
```bash
zsh tests/integration/test_zsh.sh
```

**PowerShell:**
```powershell
pwsh tests/integration/test_powershell.ps1
```

## What Gets Tested

Each test suite validates:

1. **Function Definitions** - Ensures all functions are properly defined
2. **Help Output** - Verifies help text is displayed correctly
3. **Version Information** - Checks version info is present
4. **Config File Parsing** - Tests mount configuration file loading
5. **Comment Handling** - Ensures comments and empty lines are ignored
6. **Path Resolution** - Tests tilde/home directory expansion
7. **Absolute Paths** - Verifies absolute paths remain unchanged
8. **Documentation** - Checks that options are properly documented

## CI/CD Integration

Tests run automatically on:
- Push to main branch
- Pull requests
- Manual workflow dispatch

The workflow tests on:
- **Linux** (Ubuntu) with Bash and Zsh
- **macOS** with Bash and Zsh  
- **Windows** with PowerShell

See `.github/workflows/test.yml` for the full CI configuration.

## Adding New Tests

When adding a new test:

1. Add the test to all three test files (bash, zsh, powershell)
2. Use the test helper functions (`test_start`, `test_pass`, `test_fail`)
3. Increment the test count properly
4. Update this README if testing new functionality

## Test Output

Successful test run:
```
✓ PASS: Test description
```

Failed test run:
```
✗ FAIL: Test description
```

Summary at the end shows:
- Total tests run
- Number passed (green)
- Number failed (red)
- Exit code 1 if any failures
