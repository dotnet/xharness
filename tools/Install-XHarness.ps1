### This script is a quick way to install XHarness in a local folder.
### .NET SDK is installed too, everything in a local folder which can be deleted afterwards.
###
### Use the following command to run this script from anywhere:
### iex ((New-Object System.Net.WebClient).DownloadString('https://aka.ms/get-xharness-ps1'))

param (
    [Parameter(Mandatory = $false)]
    [string]
    $Version = "*"
)

$xharness_version = "1.0.0-prerelease.$Version"

# Install .NET
Write-Host "Getting dotnet-install.ps1.."
Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile "$PSScriptRoot/dotnet-install.ps1"

Write-Host "Installing .NET SDK locally to " -NoNewline
Write-Host "$PSScriptRoot/.dotnet" -ForegroundColor Yellow
Invoke-Expression -Command "& '$PSScriptRoot/dotnet-install.ps1' -InstallDir '$PSScriptRoot/.dotnet' -Version '6.0.100'"

Write-Host ".NET installed"

Write-Host "Installing XHarness locally to " -NoNewline
Write-Host "$PSScriptRoot" -ForegroundColor Yellow

Invoke-Expression -Command "& '$PSScriptRoot/.dotnet/dotnet' tool install --tool-path '$PSScriptRoot' --version '$xharness_version' --add-source https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json Microsoft.DotNet.XHarness.CLI"

Write-Host "XHarness installed, run it using:"
Write-Host ".\xharness help"  -ForegroundColor Yellow
