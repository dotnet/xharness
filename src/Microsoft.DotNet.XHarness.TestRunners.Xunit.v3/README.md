# xunit v3 Test Runner

This project provides support for running tests with xunit v3 in XHarness.

## Seamless API Experience

As of the latest version, this package provides a seamless experience with the same class names as the xunit v2 runner. This means you can swap between the two packages without changing your code:

```csharp
// Same code works with both packages
using Microsoft.DotNet.XHarness.TestRunners.Xunit; // v2 package
using Microsoft.DotNet.XHarness.TestRunners.Xunit.v3; // v3 package

var runner = new XUnitTestRunner(logger); // Same class name in both!
```

## Package Dependencies

This project uses the following xunit v3 packages:
- `xunit.v3.extensibility.core` - Core extensibility interfaces for xunit v3
- `xunit.v3.runner.common` - Common runner utilities for xunit v3

## Key Differences from xunit v2

xunit v3 introduces significant API changes, but these are handled internally:

### Namespace Changes (Internal)
- `Xunit.Abstractions` → `Xunit.v3`

### Interface Changes (Internal)
- `ITestCase` → `IXunitTestCase`
- `ITestAssembly` → `IXunitTestAssembly`  
- `IMessageSink` → `IMessageBus`

### Architecture Changes (Internal)
- xunit v3 uses a more message-based architecture
- Test discovery and execution patterns have been updated

## Usage

To use xunit v3 instead of v2, simply reference this project instead of `Microsoft.DotNet.XHarness.TestRunners.Xunit`:

```xml
<!-- For xunit v2 -->
<ProjectReference Include="Microsoft.DotNet.XHarness.TestRunners.Xunit" />

<!-- For xunit v3 -->
<ProjectReference Include="Microsoft.DotNet.XHarness.TestRunners.Xunit.v3" />
```

Your application code remains exactly the same!

## Code Sharing Implementation

This package uses conditional compilation to share most code with the v2 package:
- Shared files use `#if USE_XUNIT_V3` to compile differently based on the target
- The `USE_XUNIT_V3` define is automatically set in this project
- This ensures consistency and reduces maintenance overhead

## Current Status

This is an initial implementation that provides the basic structure for xunit v3 support. The current implementation includes:

- ✅ Project structure and packaging
- ✅ Entry points for iOS, Android, and WASM platforms  
- ✅ Basic test runner framework
- ✅ Code sharing with v2 package using conditional compilation
- ✅ Seamless API with same class names as v2
- ⚠️ Placeholder test execution (not yet fully implemented)
- ⚠️ XSLT transformations for NUnit output formats (not yet adapted)

## Future Work

- Implement full test discovery and execution using xunit v3 APIs
- Adapt result transformations for NUnit compatibility
- Add comprehensive filtering support
- Performance optimizations

## Migration Guide

Migration is now seamless:

1. Update project references to use `Microsoft.DotNet.XHarness.TestRunners.Xunit.v3`
2. No code changes required - all class names remain the same!
3. Verify test execution works with your test assemblies
4. Any custom integrations continue to work unchanged

The goal is to provide complete API compatibility at the XHarness level while internally using the new xunit v3 APIs.