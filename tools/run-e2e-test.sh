#!/bin/bash

### This script is a quick way to run the XHarness E2E tests

set -ex

test_project="$1"

if [ -z $test_project ] || [ "-h" == "$test_project" ] || [ "--help" == "$test_project" ]; then
  echo "Usage: ./run-e2e-test.sh Apple/SimulatorInstaller.Tests.proj [--skip-build]"
  exit 2
fi

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

if [ ! -f "$test_project" ]; then
  test_project="$repo_root/tests/integration-tests/$test_project"
fi

if [ ! -f "$test_project" ]; then
  echo "$1 nor $test_project were found"
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

if [ "true" != "$skip_build" ]; then
  echo "Building Microsoft.DotNet.XHarness.CLI.1.0.0-dev.nupkg"
  "$repo_root/build.sh" -build -pack --projects "$repo_root/src/Microsoft.DotNet.XHarness.CLI/Microsoft.DotNet.XHarness.CLI.csproj"
fi

export BUILD_REASON="dev"
export BUILD_REPOSITORY_NAME="xharness"
export BUILD_SOURCEBRANCH="master"
export SYSTEM_TEAMPROJECT="dnceng"
export SYSTEM_ACCESSTOKEN=""

echo "Starting tests (logging to ./XHarness.binlog)"
"$repo_root/build.sh" -configuration Debug -restore -test -projects "$test_project" /p:RestoreUsingNugetTargets=false /bl:./XHarness.binlog
