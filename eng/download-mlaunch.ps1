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

Write-Host "mlaunch revision $commit will be installed into $TargetDir"

$tagFile = Join-Path $TargetDir ".tag-$commit"

if (Test-Path $tagFile) {
    Write-Host "mlaunch is already downloaded"
    ExitWithExitCode 0
}

$binariesRepo = Join-Path $TempDir "macios-binaries"

Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $binariesRepo
New-Item -ItemType Directory -Force -ErrorAction Stop -Path $binariesRepo

# Shallow-clone the xamarin/macios-binaries repo
Set-Location $binariesRepo

git init
git remote add origin https://github.com/xamarin/macios-binaries.git
git config core.sparseCheckout true
Set-Content -Path ".git/info/sparse-checkout" -Value "mlaunch"
git fetch --depth 1 origin $commit
git checkout FETCH_HEAD

# Copy mlaunch to the artifacts folder
Move-Item -Path "mlaunch" -Destination $TargetDir -Verbose

# Tag the version of mlaunch we have
New-Item -ItemType File -Path $tagFile

Write-Host "Finished installing mlaunch in $TargetDir"

ExitWithExitCode 0
