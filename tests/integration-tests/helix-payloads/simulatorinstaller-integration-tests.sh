#!/bin/bash

set -e

version='1.0.0-ci'

# Clean the NuGet cache from the previous 1.0.0-ci version of the tool
# TODO: This might have a better solution: https://github.com/dotnet/xharness/issues/123
echo "Cleaning the NuGet cache from the previous version of the tool..."

# Call dotnet to get rid of the welcome message since the nuget command doesn't respect the --no-logo
dotnet nuget locals All -l
cache_dirs=`dotnet nuget locals All -l | cut -d':' -f 2 | tr -d ' '`
while IFS= read -r path; do
    echo "Purging cache in $path..."
    rm -vrf "$path/microsoft.dotnet.xharness.simulatorinstaller/$version"
    rm -vrf "$path/Microsoft.DotNet.XHarness.SimulatorInstaller/$version"
done <<< "$cache_dirs"

set -x

here="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

mkdir $here/tools
cd $here/tools

dotnet new tool-manifest

dotnet tool install --no-cache --version $version --add-source .. Microsoft.DotNet.XHarness.SimulatorInstaller

export XHARNESS_DISABLE_COLORED_OUTPUT=true
export XHARNESS_LOG_WITH_TIMESTAMPS=true

dotnet tool restore --no-cache
dotnet simulator-installer list
