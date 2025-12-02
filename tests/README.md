# Tests

This directory contains tests for the copilot_here project.

## Structure

```
tests/
├── CopilotHere.UnitTests/    # .NET unit tests for the CLI app
├── integration/               # Docker/Airlock integration tests
│   └── test_airlock.sh       # Tests Airlock proxy functionality
└── README.md                  # This file
```

## Unit Tests

The CLI application is tested using TUnit (a modern .NET testing framework).

### Running Unit Tests

```bash
dotnet test
```

Tests run on all platforms (Linux, macOS, Windows) as part of CI.

### What Gets Tested

- **Argument parsing** - All CLI arguments and aliases
- **Configuration loading** - Global/local config file handling
- **Command registration** - All commands properly registered
- **Hidden aliases** - PowerShell compatibility aliases work but aren't advertised

## Integration Tests

### Airlock Tests

Tests the Docker Airlock proxy functionality:

```bash
# Run with local images (after dev-build.sh)
./tests/integration/test_airlock.sh --use-local

# Run with registry images
./tests/integration/test_airlock.sh
```

Tests validate:
- Proxy health and startup
- Allowed hosts/paths work through proxy
- Blocked hosts/paths are rejected
- CA certificate is properly shared
- Direct internet access is blocked (airlock isolation)

## CI/CD Integration

Tests run automatically on:
- Push to main branch
- Pull requests to main
- Manual workflow dispatch

See `.github/workflows/publish.yml` for the full CI configuration.
