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

$ErrorActionPreference = "Stop"

$xharness_version = "1.0.0-prerelease.$Version"

# Install .NET
Write-Host "Getting dotnet-install.ps1.." -ForegroundColor Cyan
Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile "dotnet-install.ps1"

Write-Host "Installing .NET SDK locally to " -NoNewline -ForegroundColor Cyan
Write-Host ".dotnet" -ForegroundColor Yellow
./dotnet-install.ps1 -InstallDir ./.dotnet -Version 6.0.100

Write-Host ".NET installed" -ForegroundColor Cyan
Write-Host "Installing XHarness in current folder" -ForegroundColor Cyan

./.dotnet/dotnet tool install --tool-path . --version $xharness_version --add-source https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json Microsoft.DotNet.XHarness.CLI

Write-Host "XHarness installed, run it using: " -ForegroundColor Cyan -NoNewline
Write-Host ".\xharness help" -ForegroundColor Yellow
