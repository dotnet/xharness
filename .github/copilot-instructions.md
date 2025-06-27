# XHarness Repository Copilot Instructions

## Project Overview

XHarness is a .NET command-line tool that enables running xUnit/NUnit tests on mobile platforms (Android, Apple iOS/tvOS/watchOS/xrOS/Mac Catalyst), WASI, and desktop browsers (WASM). It is part of the .NET ecosystem and is essential for cross-platform testing in the .NET Foundation projects.

**Key Capabilities:**
- Device/emulator management and discovery
- Application lifecycle management (install, run, uninstall)
- Test execution and result collection in multiple formats (text, xUnit/NUnit XML)
- Crash dump collection and symbolication
- TCP and USB connection modes
- Apple Simulator runtime installation
- Integration with Helix cloud testing infrastructure

## Architecture Overview

XHarness is organized into two main layers:

### Tooling Layer (src/)
- **Microsoft.DotNet.XHarness.CLI** - Main CLI entry point with command definitions
- **Microsoft.DotNet.XHarness.Android** - Android-specific operations using ADB
- **Microsoft.DotNet.XHarness.Apple** - Apple platform operations using mlaunch
- **Microsoft.DotNet.XHarness.iOS.Shared** - Apple mobile platforms shared functionality
- **Microsoft.DotNet.XHarness.Common** - Core building blocks (logging, execution, utilities, diagnostics)

### Application Layer (src/)
- **Microsoft.DotNet.XHarness.TestRunners.Common** - Test discovery, execution, and results aggregation
- **Microsoft.DotNet.XHarness.TestRunners.Xunit** - XUnit framework integration
- **Microsoft.DotNet.XHarness.TestRunners.NUnit** - NUnit framework integration
- **Microsoft.DotNet.XHarness.DefaultAndroidEntryPoint.Xunit** - Default Android entry point

## Command Structure

XHarness follows a platform → command pattern:
```bash
xharness [platform] [command] [options]
```

### Supported Platforms and Commands:

**Android Commands:**
- `AndroidTest`, `AndroidDevice`, `AndroidInstall`, `AndroidRun`, `AndroidUninstall`, `AndroidAdb`, `AndroidState`

**Apple Commands:**
- `AppleTest`, `AppleRun`, `AppleInstall`, `AppleUninstall`, `AppleJustTest`, `AppleJustRun`, `AppleDevice`, `AppleMlaunch`, `AppleState`

**Apple Simulator Commands:**
- `List`, `Find`, `Install`, `ResetSimulator`

**WASM Commands:**
- `WasmTest`, `WasmTestBrowser`, `WebServer`

**WASI Commands:**
- `WasiTest`

## Development Guidelines

### System Requirements
- .NET 6+ for development and runtime
- macOS with full Xcode installation for Apple scenarios
- Linux/macOS/Windows for Android scenarios
- Linux for browser scenarios

### Build System
- Use `./build.sh` (Linux/macOS) or `Build.cmd` (Windows) for proper SDK setup
- Alternative: `dotnet build XHarness.sln` (requires correct .NET version)
- Integration with Arcade SDK for .NET Foundation build standards
- Azure DevOps pipelines for CI/CD

### Key Dependencies
- **ADB (Android Debug Bridge)** - Required for Android operations
- **mlaunch** - Required for Apple platform operations
- **Helix SDK** - For cloud testing integration
- Downloaded automatically during CLI build process

### Exit Codes
XHarness uses standardized exit codes (see `src/Microsoft.DotNet.XHarness.Common/CLI/ExitCode.cs`):
- `0` - SUCCESS
- `1` - TESTS_FAILED
- `70` - TIMED_OUT
- `78` - PACKAGE_INSTALLATION_FAILURE
- `80` - APP_CRASH
- `81` - DEVICE_NOT_FOUND
- And many more specific failure scenarios

## Platform-Specific Knowledge

### Android Development
- Uses ADB for device communication
- Supports APK installation and logcat collection
- Package name-based application identification
- Emulator and physical device support

### Apple Development
- Uses mlaunch for device/simulator communication
- Supports .app bundle and .ipa installations
- Requires proper code signing and provisioning profiles
- Complex simulator runtime management
- TCP connection workarounds for test result streaming

### WASM/WASI Development
- Browser-based test execution
- WebAssembly runtime requirements
- Custom web server for test hosting

## Testing Strategy

### Unit Tests
- Located in `tests/` directory with platform-specific test projects
- Follow naming convention: `Microsoft.DotNet.XHarness.[Component].Tests`
- Use xUnit framework consistently

### Integration Tests
- Located in `tests/integration-tests/`
- E2E tests that use Helix cloud infrastructure
- Test real device/simulator scenarios
- Use `./tools/run-e2e-test.sh` for execution

### Test Runners
- Applications must include TestRunner library for `apple test` command
- TestRunner handles environmental variables and TCP connections
- Alternative: Use `apple run` for apps without TestRunner

## Common Patterns and Conventions

### Command Implementation
- Extend `XHarnessCommand<T>` abstract base class
- Implement required properties: `CommandUsage`, `CommandDescription`, `Arguments`
- Use dependency injection for logging and services
- Return appropriate `ExitCode` enum values

### Logging
- Multiple logger types: `ConsoleLogger`, `FileLogger`, `MemoryLogger`, `AggregatedLogs`, `CallbackLogger`
- Console logger is default for commands
- File logger used for mlaunch and adb commands
- Memory logger used by platform-specific command runners

### Error Handling
- Use specific exception types in `Microsoft.DotNet.XHarness.Common.CLI`
- `NoDeviceFoundException` for device discovery failures
- Proper exit code mapping for different failure scenarios

### Environmental Variables
- `XHARNESS_DISABLE_COLORED_OUTPUT` - Disable colored logging
- `XHARNESS_LOG_WITH_TIMESTAMPS` - Enable timestamps
- `XHARNESS_LOG_TEST_START` - Log test start messages
- `XHARNESS_MLAUNCH_PATH` - Custom mlaunch path for development

## File and Directory Structure

```
/
├── src/                          # Source code
├── tests/                        # Unit and integration tests
├── docs/                         # Documentation
├── eng/                          # Build and engineering files
├── tools/                        # Development tools and scripts
├── azure-pipelines*.yml         # CI/CD pipeline definitions
├── XHarness.sln                 # Main solution file
├── build.sh / Build.cmd         # Build scripts
└── README.md                    # Main documentation
```

## Troubleshooting Guidelines

### Common Issues
1. **Apple unit tests not running**: Ensure TestRunner is included in app bundle
2. **iOS/tvOS device timeouts**: Use `--signal-app-end` flag and ensure app logs the `RUN_END_TAG`
3. **Build failures**: Check .NET SDK version and use provided build scripts
4. **Device not found**: Verify device connection and platform-specific tooling (ADB/mlaunch)

### Debugging Tips
- Use appropriate verbosity levels for logging
- Check device/simulator state before test execution
- Verify app signing and provisioning for Apple platforms
- Monitor TCP connections for test result streaming

## Development Workflow

### For Bug Fixes
1. Identify affected platform and component
2. Create unit tests to reproduce the issue
3. Implement minimal fix in appropriate layer
4. Ensure no regression in existing functionality
5. Update integration tests if needed

### For New Features
1. Understand platform-specific requirements
2. Design feature following existing command patterns
3. Implement with proper error handling and exit codes
4. Add comprehensive tests (unit and integration)
5. Update documentation and help text

### Code Quality
- Follow existing naming conventions and code patterns
- Use dependency injection for testability
- Implement proper logging throughout
- Handle platform-specific edge cases
- Maintain backwards compatibility when possible

## Self-Improvement Instructions

**IMPORTANT**: If you discover any issues, gaps, or outdated information in these instructions while working on XHarness issues, you must update this document with your new knowledge and learnings. This includes:

1. **New platform-specific quirks or workarounds discovered**
2. **Additional environmental variables or configuration options**
3. **Updated build procedures or dependency requirements**
4. **New testing patterns or debugging techniques**
5. **Command structure changes or new platform support**
6. **Performance optimization patterns**
7. **Security considerations or best practices**

When updating these instructions:
- Add specific examples and code snippets where helpful
- Include version information for any platform-specific requirements
- Document the context and scenario where the knowledge applies
- Maintain the existing structure and organization
- Test your changes to ensure accuracy

Your goal is to continuously improve these instructions to become the most effective autonomous agent for XHarness development, capable of solving issues, fixing bugs, and implementing new features efficiently.

---

*These instructions are designed to help you understand and work effectively with the XHarness codebase. Keep them updated as you learn more about the project.*