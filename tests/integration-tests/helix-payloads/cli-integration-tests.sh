#!/bin/bash

set -e

if [ "$#" -ne 2 ] || [ -z "$1" ] || [ -z "$2" ]; then
    echo "The script expects 2 arguments: where to store result logs and where to upload test results (ie. \$HELIX_WORKITEM_UPLOAD_ROOT \$HELIX_WORKITEM_ROOT)" 1>2
    exit 1
fi

set -x

here="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

export DOTNET_ROOT=$(dirname $(which dotnet))
dotnet tool install --no-cache --tool-path "$here/xharness-cli" --version "1.0.0-ci" --add-source "$here" Microsoft.DotNet.XHarness.CLI

# TODO - Package the app bundle here using `dotnet xharness ios package ..`
# For now we download a pre-bundled app:
curl -L --output "$here/app.zip" "https://netcorenativeassets.blob.core.windows.net/resource-packages/external/macos/test-ios-app/System.Numerics.Vectors.Tests.app.zip"
app_name='System.Numerics.Vectors.Tests.app'

tar -xzf app.zip

set +e

# Restart the simulator to make sure it is tied to the right user session
xcode_path=`xcode-select -p`
sudo pkill -9 -f "$xcode_path/Applications/Simulator.app"
open -a "$xcode_path/Applications/Simulator.app"

export XHARNESS_DISABLE_COLORED_OUTPUT=true
export XHARNESS_LOG_WITH_TIMESTAMPS=true

"$here/xharness-cli/xharness" ios test \
    --app="$here/$app_name"            \
    --output-directory="$1"            \
    --targets=ios-simulator-64         \
    --timeout=600                      \
    --launch-timeout=360               \
    --xcode=/Applications/Xcode115.app \
    -v

result=$?

# Kill the simulator after we're done
sudo pkill -9 -f "$xcode_path/Applications/Simulator.app"

# iPhone simulator logs are published under root and cannot be read
chmod 0644 "$1"/*.log "$1"/*.xml

test_results=`ls $1/xunit-*.xml`

if [ ! -f "$test_results" ]; then
    echo "Failed to find xUnit tests results in the output directory. Existing files:"
    ls -la "$1"
    exit 1
fi

echo "Found test results in '$1/$test_results'. Renaming to testResults.xml"

# Prepare test results for Helix to pick up
mv "$test_results" "$2/testResults.xml"

if ! cat "$2/testResults.xml" | grep 'collection total="19" passed="19" failed="0" skipped="0"'; then
    echo "Failed to detect result line"
    exit 1
fi

exit $result
