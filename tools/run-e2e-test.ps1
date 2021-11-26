<#
.SYNOPSIS
  This script is a quick way to run the XHarness E2E tests.
  These test are located in tests/integration-tests and require the Arcade and Helix SDK.
  To run them, you need to invoke these through MSBuild which makes the process a bit cumbersome.
  This script should make things easier.

.EXAMPLE
  .\run-e2e-test.ps1 -TestProject Apple/SimulatorInstaller.Tests.proj [-SkipBuild]
#>

param (
    <# Path to the test project. Can be also relative to tests/integration-tests, e.g. Apple/Device.iOS.Tests.proj #>
    [Parameter(Mandatory = $true)]
    [string]
    $TestProject,

    <# Skip re-building the local package #>
    [switch]
    $SkipBuild = $false
)

$repoRoot = Resolve-Path "$PSScriptRoot\.."

function Write-Projects {
    $testRoot = "$repoRoot\tests\integration-tests\"
    $files = Get-ChildItem -Recurse -Include *Tests.proj -Path $testRoot | ForEach-Object { $_.FullName.Substring($testRoot.Length) }

    Write-Output "Possible options:"
    foreach ($item in $files) {
        " - $item"
    }
}

if (-not(Test-Path -Path $TestProject -PathType Leaf)) {
    $TestProject = "$repoRoot\tests\integration-tests\$TestProject"

    if (-not(Test-Path -Path $TestProject -PathType Leaf)) {
        Write-Error "The file $TestProject not found"
        Write-Projects
        Exit 1
    }
}

if ($SkipBuild) {
    Write-Host -ForegroundColor Cyan "> Skipping build"
} else {
    Write-Host -ForegroundColor Cyan "> Building Microsoft.DotNet.XHarness.CLI.1.0.0-dev.nupkg"

    Remove-Item -Recurse -ErrorAction SilentlyContinue "$repoRoot\artifacts\tmp\Debug\Microsoft.DotNet.XHarness.CLI"
    Remove-Item -Recurse -ErrorAction SilentlyContinue "$repoRoot\artifacts\artifacts\packages"

    & "$repoRoot\Build.cmd" -pack -projects "$repoRoot\src\Microsoft.DotNet.XHarness.CLI\Microsoft.DotNet.XHarness.CLI.csproj"
}

$Env:BUILD_REASON = "pr"
$Env:BUILD_REPOSITORY_NAME = "arcade"
$Env:BUILD_SOURCEBRANCH = "test"
$Env:SYSTEM_TEAMPROJECT = "dnceng"
$Env:SYSTEM_ACCESSTOKEN = ""

Write-Host -ForegroundColor Cyan "> Starting tests (logging to XHarness.binlog)"

& "$repoRoot\Build.cmd" -configuration Debug -test -projects "$TestProject" /p:RestoreUsingNugetTargets=false /bl:.\XHarness.binlog
