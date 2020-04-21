<#
    Commandlet downloads specific revision of the mlaunch binary from xamarin/macios-binaries repository.
    This binary is then bundled with XHarness inside the NuGet.

    Revision is cached in a temp dir and re-used for new builds.
#>
[CmdletBinding(PositionalBinding=$false)]
Param(
    [ValidateNotNullOrEmpty()]
    [Parameter(Mandatory=$True)]
    [string] $Commit,

    [ValidateNotNullOrEmpty()]
    [Parameter(Mandatory=$True)]
    [string] $TargetDir
)

. $PSScriptRoot\common\tools.ps1

New-Item -Path $TargetDir -ItemType Directory -ErrorAction SilentlyContinue

Write-Host "Getting mlaunch revision $commit into $TargetDir"

$tagFile = Join-Path $TargetDir "$commit.tag"

if (Test-Path $tagFile) {
    Write-Host "mlaunch is already downloaded"
    ExitWithExitCode 0
}

$binariesRepo = Join-Path $TempDir "macios-binaries"
$tagFileInRepo = Join-Path $binariesRepo "$commit.tag"

if (Test-Path $binariesRepo) {
    if (Test-Path $tagFileInRepo) {
        $path = Join-Path $binariesRepo "mlaunch"
        $path = Join-Path $path "*"

        # Copy mlaunch to the target folder
        Copy-Item -Path $path -Destination $TargetDir -Recurse -Verbose

        # Tag the version of mlaunch we have
        New-Item -ItemType File -Path $tagFile

        Write-Host "Finished downloading mlaunch from cache"
        ExitWithExitCode 0
    }

    Write-Host "Found cached repository but different version was checked out. Downloading again for $commit..."
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $binariesRepo
}

Write-Host "Cloning the xamarin-binaries repository. This might take few minutes.."

New-Item -ItemType Directory -Force -ErrorAction Stop -Path $binariesRepo

# Shallow-clone the xamarin/macios-binaries repo
Set-Location $binariesRepo

# Workaround for https://github.com/dahlbyk/posh-git/issues/109
$env:GIT_REDIRECT_STDERR = '2>&1'
$ErrorActionPreference = "Continue"

git init
git remote add origin https://github.com/xamarin/macios-binaries.git
git config core.sparseCheckout true
git config core.autocrlf false
git config core.eol lf
Set-Content -Path ".git/info/sparse-checkout" -Value "mlaunch"
git fetch -v --depth 1 origin $commit
git checkout FETCH_HEAD

# Clean what we don't need
Remove-Item -Path (Join-Path $binariesRepo "mlaunch/lib/mlaunch/mlaunch.app/Contents/MacOS/mlaunch.dSYM") -Recurse -Force -Verbose
Get-ChildItem -Path (Join-Path $binariesRepo "mlaunch/lib/mlaunch/mlaunch.app/Contents/MonoBundle/*.pdb") | ForEach-Object { Remove-Item -Path $_.FullName -Verbose }
Get-ChildItem -Path (Join-Path $binariesRepo "mlaunch/lib/mlaunch/mlaunch.app/Contents/MonoBundle/*.mdb") | ForEach-Object { Remove-Item -Path $_.FullName -Verbose }

New-Item -ItemType File -Path $tagFileInRepo

# Copy mlaunch to the target folder
Copy-Item -Path (Join-Path $binariesRepo (Join-Path "mlaunch" "*")) -Destination $TargetDir -Recurse -Verbose

# Tag the version of mlaunch we have
New-Item -ItemType File -Path $tagFile -ErrorAction "SilentlyContinue"

Write-Host "Finished installing mlaunch in $TargetDir"

ExitWithExitCode 0
