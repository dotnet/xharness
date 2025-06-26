# xunit v3 Sample

This sample demonstrates how to use the new xunit v3 test runner in XHarness.

## Running the Sample

```bash
dotnet run
```

## Key Features Demonstrated

1. **xunit v3 Test Runner Usage**: Shows how to create and use the `XunitV3TestRunner`
2. **Entry Point**: Demonstrates the `WasmApplicationEntryPoint` for web scenarios  
3. **Test Execution**: Runs actual tests and shows result output
4. **Logging Integration**: Shows how the logging system works

## Expected Output

The sample will:
1. Create a test runner instance
2. Discover and run tests in the current assembly
3. Generate XML results compatible with xunit format
4. Display both structured results and log output

## Comparison with xunit v2

To see the difference, compare this with a similar v2 implementation:

```csharp
// v2
using Microsoft.DotNet.XHarness.TestRunners.Xunit;
var runner = new XUnitTestRunner(logger);

// v3  
using Microsoft.DotNet.XHarness.TestRunners.Xunit.v3;
var runner = new XunitV3TestRunner(logger);
```

The API is designed to be similar to ease migration between versions.