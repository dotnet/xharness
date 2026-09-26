# Quick Start: Code Coverage in XHarness

## 🚀 Run Coverage (Simplest)

```bash
./run-coverage.sh
```

That's it! Coverage will be generated in `artifacts/coverage/`.

---

## 📊 View Coverage Report (HTML)

### One-time setup:
```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
```

### Generate and view report:
```bash
# Generate HTML report
reportgenerator \
  -reports:"artifacts/coverage/**/coverage.cobertura.xml" \
  -targetdir:"artifacts/coverage/report" \
  -reporttypes:Html

# Open in browser (macOS)
open artifacts/coverage/report/index.html

# Or Linux
xdg-open artifacts/coverage/report/index.html
```

---

## 🎯 Quick Commands

### Run with different configuration:
```bash
./run-coverage.sh Release
```

### Run with specific format:
```bash
./run-coverage.sh Debug opencover
```

### Run a single test project:
```bash
dotnet test tests/Microsoft.DotNet.XHarness.CLI.Tests/Microsoft.DotNet.XHarness.CLI.Tests.csproj \
  --collect:"XPlat Code Coverage" \
  --results-directory artifacts/coverage \
  --settings tests/coverlet.runsettings
```

### View summary in terminal:
```bash
reportgenerator \
  -reports:"artifacts/coverage/**/coverage.cobertura.xml" \
  -reporttypes:TextSummary
```

---

## 📝 What's Included

- ✅ Coverlet configured for all test projects
- ✅ Scripts for Linux/macOS and Windows
- ✅ Multiple output formats (Cobertura, OpenCover, JSON)
- ✅ Excludes test assemblies and auto-properties
- ✅ Optimized settings for best performance
- ✅ Comprehensive documentation

---

## 📚 More Information

See [docs/code-coverage.md](docs/code-coverage.md) for:
- Advanced usage
- CI/CD integration
- VS Code extensions
- Troubleshooting
- Configuration options

---

## 🎨 VS Code Integration (Optional)

Install the [Coverage Gutters](https://marketplace.visualstudio.com/items?itemName=ryanluker.vscode-coverage-gutters) extension to see coverage directly in your editor:

1. Install the extension
2. Run coverage: `./run-coverage.sh`
3. Press `Ctrl+Shift+7` (or `Cmd+Shift+7` on Mac) to toggle coverage display
4. Coverage will be shown as colored lines in the editor gutter
