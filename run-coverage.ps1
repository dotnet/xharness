[CmdletBinding(PositionalBinding=$false)]
Param(
  [string]$configuration = "Debug",
  [string]$format = "cobertura"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$RepoRoot = Join-Path $PSScriptRoot ""
$OutputDir = Join-Path $RepoRoot "artifacts\coverage"
$RunSettings = Join-Path $RepoRoot "tests\coverlet.runsettings"

Write-Host "Running tests with code coverage..." -ForegroundColor Cyan
Write-Host "Configuration: $configuration"
Write-Host "Output format: $format"
Write-Host ""

# Clean previous coverage results
if (Test-Path $OutputDir) {
    Remove-Item -Recurse -Force $OutputDir
}
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# Run tests with coverage
& dotnet test "$RepoRoot\XHarness.slnx" `
    --configuration $configuration `
    --collect:"XPlat Code Coverage" `
    --results-directory $OutputDir `
    --settings:$RunSettings `
    -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=$format

Write-Host ""
Write-Host "Coverage results generated in: $OutputDir" -ForegroundColor Green
Write-Host ""
Write-Host "To generate an HTML report, install ReportGenerator:"
Write-Host "  dotnet tool install -g dotnet-reportgenerator-globaltool"
Write-Host ""
Write-Host "Then run:"
Write-Host "  reportgenerator -reports:`"$OutputDir\**\coverage.$format.xml`" -targetdir:`"$OutputDir\report`" -reporttypes:Html"
Write-Host ""
