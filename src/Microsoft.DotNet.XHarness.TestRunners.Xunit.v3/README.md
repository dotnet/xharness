# xunit v3 Test Runner

This project provides support for running tests with xunit v3 in XHarness.

## Package Dependencies

This project uses the following xunit v3 packages:
- `xunit.v3.extensibility.core` - Core extensibility interfaces for xunit v3
- `xunit.v3.runner.common` - Common runner utilities for xunit v3

## Key Differences from xunit v2

xunit v3 introduces significant API changes:

### Namespace Changes
- `Xunit.Abstractions` → `Xunit.v3`

### Interface Changes
- `ITestCase` → `IXunitTestCase`
- `ITestAssembly` → `IXunitTestAssembly`  
- `IMessageSink` → `IMessageBus`

### Architecture Changes
- xunit v3 uses a more message-based architecture
- Test discovery and execution patterns have been updated

## Usage

To use xunit v3 instead of v2, reference this project instead of `Microsoft.DotNet.XHarness.TestRunners.Xunit`:

```xml
<ProjectReference Include="Microsoft.DotNet.XHarness.TestRunners.Xunit.v3" />
```

## Current Status

This is an initial implementation that provides the basic structure for xunit v3 support. The current implementation includes:

- ✅ Project structure and packaging
- ✅ Entry points for iOS, Android, and WASM platforms  
- ✅ Basic test runner framework
- ⚠️ Placeholder test execution (not yet fully implemented)
- ⚠️ XSLT transformations for NUnit output formats (not yet adapted)

## Future Work

- Implement full test discovery and execution using xunit v3 APIs
- Adapt result transformations for NUnit compatibility
- Add comprehensive filtering support
- Performance optimizations

## Migration Guide

When migrating from v2 to v3:

1. Update project references to use `Microsoft.DotNet.XHarness.TestRunners.Xunit.v3`
2. Verify test execution works with your test assemblies
3. Update any custom integrations that depend on xunit-specific APIs

The goal is to maintain API compatibility at the XHarness level while internally using the new xunit v3 APIs.