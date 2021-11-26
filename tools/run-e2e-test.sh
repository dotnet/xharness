#!/bin/bash

### This script is a quick way to run the XHarness E2E tests.
### These test are located in tests/integration-tests and require the Arcade and Helix SDK.
### To run them, you need to invoke these through MSBuild which makes the process a bit cumbersome.
### This script should make things easier.
###
### Usage: ./run-e2e-test.sh Apple/SimulatorInstaller.Tests.proj [--skip-build]

test_project="$1"

COLOR_RED=$(tput setaf 1 2>/dev/null || true)
COLOR_CYAN=$(tput setaf 6 2>/dev/null || true)
COLOR_CLEAR=$(tput sgr0 2>/dev/null || true)
COLOR_RESET=uniquesearchablestring
FAILURE_PREFIX=
if test -z "$COLOR_RED"; then FAILURE_PREFIX="** failure ** "; fi

function fail () {
  echo "$FAILURE_PREFIX${COLOR_RED}${1//${COLOR_RESET}/${COLOR_RED}}${COLOR_CLEAR}"
}

function highlight () {
  echo "$FAILURE_PREFIX${COLOR_CYAN}${1//${COLOR_RESET}/${COLOR_CYAN}}${COLOR_CLEAR}"
}

function print_projects() {
  echo "Possible options:"
  prefix=$(echo "$repo_root/tests/integration-tests/" | sed "s/\//\\\\\//g")
  find "$repo_root/tests/integration-tests" -type f -name "*.proj" | sed "s/$prefix/  - /"
}

# Get current path
source="${BASH_SOURCE[0]}"
# resolve $source until the file is no longer a symlink
while [[ -h "$source" ]]; do
  script_root="$( cd -P "$( dirname "$source" )" && pwd )"
  source="$(readlink "$source")"
  # if $source was a relative symlink, we need to resolve it relative to the path where the
  # symlink file was located
  [[ $source != /* ]] && source="$script_root/$source"
done

here="$( cd -P "$( dirname "$source" )" && pwd )"
repo_root="$( cd -P "$( dirname "$here" )" && pwd )"

if [ -z "$test_project" ] || [ "-h" == "$test_project" ] || [ "--help" == "$test_project" ]; then
  fail "Usage: ./run-e2e-test.sh Apple/SimulatorInstaller.Tests.proj [--skip-build]"
  print_projects
  exit 2
fi

if [ ! -f "$test_project" ]; then
  test_project="$repo_root/tests/integration-tests/$test_project"
fi

if [ ! -f "$test_project" ]; then
  fail "File $1 not found"
  fail "File $test_project not found"
  print_projects
  exit 1
fi

shift

skip_build=false

while (($# > 0)); do
  lowerI="$(echo "$1" | awk '{print tolower($0)}')"
  case $lowerI in
    --skip-build)
      shift
      skip_build=true
      ;;
  esac
  shift
done

set -e

if [ "true" != "$skip_build" ]; then
  highlight "> Building Microsoft.DotNet.XHarness.CLI.1.0.0-dev.nupkg"
  rm -rf "$repo_root/artifacts/tmp/Debug/Microsoft.DotNet.XHarness.CLI" "$repo_root/artifacts/packages"
  "$repo_root/build.sh" -build -pack --projects "$repo_root/src/Microsoft.DotNet.XHarness.CLI/Microsoft.DotNet.XHarness.CLI.csproj"
else
  highlight "> Skipping build"
fi

export BUILD_REASON="dev"
export BUILD_REPOSITORY_NAME="xharness"
export BUILD_SOURCEBRANCH="master"
export SYSTEM_TEAMPROJECT="dnceng"
export SYSTEM_ACCESSTOKEN=""

highlight "> Starting tests (logging to XHarness.binlog)"
"$repo_root/build.sh" -configuration Debug -restore -test -projects "$test_project" /p:RestoreUsingNugetTargets=false /bl:./XHarness.binlog
