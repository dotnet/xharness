# xunit Version Selection in XHarness

XHarness now supports both xunit v2 and xunit v3. This document helps you choose the right version for your project.

## Quick Reference

| Feature | xunit v2 | xunit v3 |
|---------|----------|----------|
| **Package** | `Microsoft.DotNet.XHarness.TestRunners.Xunit` | `Microsoft.DotNet.XHarness.TestRunners.Xunit.v3` |
| **Stability** | ✅ Stable (2.9.3) | ⚠️ Prerelease (3.0.0-pre.25) |
| **API Compatibility** | ✅ Mature | ⚠️ Breaking changes |
| **Performance** | ✅ Proven | 🔄 To be evaluated |
| **Features** | ✅ Full implementation | ⚠️ Basic implementation |

## When to Use xunit v2

**Recommended for:**
- Production applications
- Existing projects already using xunit v2
- Projects requiring stable, battle-tested functionality
- Full feature compatibility (filtering, result transformations, etc.)

**Example project reference:**
```xml
<ProjectReference Include="Microsoft.DotNet.XHarness.TestRunners.Xunit" />
```

## When to Use xunit v3

**Recommended for:**
- New projects that want to adopt the latest xunit
- Projects that need xunit v3 specific features
- Early adopters willing to work with prerelease software
- Testing and evaluation scenarios

**Example project reference:**
```xml
<ProjectReference Include="Microsoft.DotNet.XHarness.TestRunners.Xunit.v3" />
```

## Migration Path

### From v2 to v3

1. **Update project reference:**
   ```xml
   <!-- Remove -->
   <ProjectReference Include="Microsoft.DotNet.XHarness.TestRunners.Xunit" />
   
   <!-- Add -->
   <ProjectReference Include="Microsoft.DotNet.XHarness.TestRunners.Xunit.v3" />
   ```

2. **Update entry point namespace:**
   ```csharp
   // v2
   using Microsoft.DotNet.XHarness.TestRunners.Xunit;
   
   // v3  
   using Microsoft.DotNet.XHarness.TestRunners.Xunit.v3;
   ```

3. **Test thoroughly** - v3 uses different underlying APIs

### From v3 to v2 (rollback)

Simply reverse the above steps. The XHarness-level APIs are designed to be compatible.

## Technical Differences

### Package Dependencies

**xunit v2:**
- `xunit.extensibility.execution` (2.9.3)
- `xunit.runner.utility` (2.9.3)

**xunit v3:**
- `xunit.v3.extensibility.core` (3.0.0-pre.25)
- `xunit.v3.runner.common` (3.0.0-pre.25)

### Key API Changes in v3

- Namespace: `Xunit.Abstractions` → `Xunit.v3`
- `ITestCase` → `IXunitTestCase`
- `ITestAssembly` → `IXunitTestAssembly`
- `IMessageSink` → `IMessageBus`

## Current Implementation Status

### xunit v2 ✅
- Full test discovery and execution
- Complete filtering support
- XSLT result transformations (NUnit v2/v3)
- Platform support (iOS, Android, WASM)
- Performance optimizations

### xunit v3 ⚠️
- Basic project structure
- Platform entry points
- Placeholder test execution
- Limited filtering (copied from v2, needs adaptation)
- No XSLT transformations yet

## Support and Contributions

- **xunit v2**: Fully supported, stable
- **xunit v3**: Community contributions welcome to improve the implementation

Both versions are maintained in parallel to provide flexibility during the xunit v3 transition period.