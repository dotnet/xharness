# Code Coverage in XHarness

This document explains how to generate and view code coverage reports for the XHarness project.

## Prerequisites

- .NET 6+ SDK
- (Optional) [ReportGenerator](https://github.com/danielpalme/ReportGenerator) for HTML reports:
  ```bash
  dotnet tool install -g dotnet-reportgenerator-globaltool
  ```

## Quick Start

### Running Tests with Coverage (Linux/macOS)

```bash
./run-coverage.sh [Configuration] [Format]
```

**Examples:**
```bash
# Run with default settings (Debug, cobertura)
./run-coverage.sh

# Run Release configuration with OpenCover format
./run-coverage.sh Release opencover

# Run Debug with JSON format
./run-coverage.sh Debug json
```

### Running Tests with Coverage (Windows)

```powershell
.\run-coverage.ps1 -configuration [Configuration] -format [Format]
```

**Examples:**
```powershell
# Run with default settings (Debug, cobertura)
.\run-coverage.ps1

# Run Release configuration with OpenCover format
.\run-coverage.ps1 -configuration Release -format opencover
```

## Coverage Output Formats

Coverlet supports multiple output formats:
- **cobertura** - Cobertura XML format (default, best for CI/CD)
- **opencover** - OpenCover XML format
- **json** - JSON format
- **lcov** - LCOV format

## Viewing Coverage Reports

### Option 1: Generate HTML Report (Recommended)

After running tests with coverage:

```bash
# Install ReportGenerator (one-time)
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate HTML report from Cobertura format
reportgenerator \
  -reports:"artifacts/coverage/**/coverage.cobertura.xml" \
  -targetdir:"artifacts/coverage/report" \
  -reporttypes:Html

# Open the report
open artifacts/coverage/report/index.html  # macOS
xdg-open artifacts/coverage/report/index.html  # Linux
start artifacts/coverage/report/index.html  # Windows
```

### Option 2: Use VS Code Extensions

Install one of these VS Code extensions:
- [Coverage Gutters](https://marketplace.visualstudio.com/items?itemName=ryanluker.vscode-coverage-gutters)
- [Code Coverage](https://marketplace.visualstudio.com/items?itemName=markis.code-coverage)

These extensions will show coverage directly in the editor.

### Option 3: Use Command-Line Summary

```bash
# Install reportgenerator if not already installed
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate summary to console
reportgenerator \
  -reports:"artifacts/coverage/**/coverage.cobertura.xml" \
  -reporttypes:TextSummary
```

## Manual Test Execution with Coverage

If you prefer more control, you can run individual test projects:

```bash
# Run a specific test project with coverage
dotnet test tests/Microsoft.DotNet.XHarness.CLI.Tests/Microsoft.DotNet.XHarness.CLI.Tests.csproj \
  --collect:"XPlat Code Coverage" \
  --results-directory artifacts/coverage \
  --settings tests/coverlet.runsettings
```

## Coverage Configuration

Coverage behavior is configured in `tests/coverlet.runsettings`:

### Key Settings:

- **Exclude**: Assemblies/types to exclude from coverage (e.g., test assemblies, third-party libraries)
- **ExcludeByAttribute**: Exclude members with specific attributes (e.g., `[GeneratedCode]`)
- **ExcludeByFile**: Exclude specific files or patterns
- **SkipAutoProps**: Skip auto-implemented properties
- **UseSourceLink**: Use SourceLink for better source file mapping

### Customizing Coverage

Edit `tests/coverlet.runsettings` to:
- Add more exclusions
- Change output formats
- Adjust threshold values
- Include/exclude specific assemblies

Example exclusions:
```xml
<Exclude>[*.Tests]*,[xunit.*]*,[Moq]*,[System.*]*</Exclude>
```

## Integration with CI/CD

### Azure Pipelines

Add this to your pipeline YAML:

```yaml
- task: DotNetCoreCLI@2
  displayName: 'Run Tests with Coverage'
  inputs:
    command: 'test'
    projects: '**/*Tests.csproj'
    arguments: '--configuration $(buildConfiguration) --collect:"XPlat Code Coverage" --settings:tests/coverlet.runsettings'

- task: PublishCodeCoverageResults@1
  displayName: 'Publish Coverage Results'
  inputs:
    codeCoverageTool: 'Cobertura'
    summaryFileLocation: '$(Agent.TempDirectory)/**/coverage.cobertura.xml'
```

### GitHub Actions

Add this to your workflow:

```yaml
- name: Run Tests with Coverage
  run: ./run-coverage.sh Release cobertura

- name: Upload Coverage to Codecov
  uses: codecov/codecov-action@v3
  with:
    files: artifacts/coverage/**/coverage.cobertura.xml
    fail_ci_if_error: true
```

## Coverage Thresholds

You can enforce minimum coverage thresholds by adding to test project `.csproj` files:

```xml
<PropertyGroup>
  <CoverletOutputFormat>cobertura</CoverletOutputFormat>
  <Threshold>80</Threshold>
  <ThresholdType>line,branch,method</ThresholdType>
  <ThresholdStat>total</ThresholdStat>
</PropertyGroup>
```

This will fail the build if coverage falls below the threshold.

## Troubleshooting

### Coverage is 0% or Empty

1. Ensure test assemblies are being excluded: `<Exclude>[*.Tests]*</Exclude>`
2. Check that source projects are referenced, not just DLLs
3. Verify the `IncludeTestAssembly` setting is `false`

### Missing Source Files in Report

1. Enable SourceLink: `<UseSourceLink>true</UseSourceLink>`
2. Ensure PDB files are being generated
3. Check that source file paths are correct

### Performance Issues

1. Use `<SingleHit>true</SingleHit>` to record only first hit per line
2. Exclude more assemblies/files
3. Run specific test projects instead of entire solution

## Additional Resources

- [Coverlet Documentation](https://github.com/coverlet-coverage/coverlet)
- [ReportGenerator Documentation](https://github.com/danielpalme/ReportGenerator)
- [Code Coverage Best Practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-code-coverage)
