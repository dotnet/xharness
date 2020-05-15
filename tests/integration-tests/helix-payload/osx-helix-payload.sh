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
    rm -vrf "$path/microsoft.dotnet.xharness.cli/$version"
    rm -vrf "$path/Microsoft.DotNet.XHarness.CLI/$version"
done <<< "$cache_dirs"

set -x

here="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

# TODO - Package the app bundle here using `dotnet xharness ios package ..`
# For now we download a pre-bundled app:
curl -L --output $here/app.zip "https://netcorenativeassets.blob.core.windows.net/resource-packages/external/macos/test-ios-app/System.Numerics.Vectors.Tests.app.zip"
app_name='System.Numerics.Vectors.Tests.app'

tar -xzf app.zip

mkdir $here/tools
cd $here/tools

dotnet new tool-manifest

dotnet tool install --no-cache --version $version --add-source .. Microsoft.DotNet.XHarness.CLI

export XHARNESS_DISABLE_COLORED_OUTPUT=true
export XHARNESS_LOG_WITH_TIMESTAMPS=true

# Restart the simulator to make sure it is tied to the right user session
xcode_path=`xcode-select -p`
pid=`ps aux | grep $xcode_path/Applications/Simulator.app | grep -v grep | tr -s ' ' | cut -d ' ' -f 2`
if [ ! -z "$pid" ]; then
    sudo kill $pid
fi

open -a $xcode_path/Applications/Simulator.app

dotnet tool restore --no-cache
dotnet xharness ios test            \
    --app="$here/$app_name"         \
    --output-directory="$1"         \
    --targets=ios-simulator-64      \
    --timeout=600                   \
    --launch-timeout=360            \
    --communication-channel=Network \
    -v

result=$?

set +e

chmod 0666 $1/*

test_results=`ls $1/xunit-*.xml`

if [ ! -f "$test_results" ]; then
    echo "Failed to find xUnit tests results in the output directory. Existing files:"
    ls -la $1
    exit 1
fi

echo "Found test results in $1/$test_results. Renaming to testResults.xml"

# Prepare test results for Helix to pick up
cp $test_results $2/testResults.xml

if ! cat $2/testResults.xml | grep 'collection total="19" passed="19" failed="0" skipped="0"'; then
    echo "Failed to detect result line"
    exit 1
fi

exit $result
