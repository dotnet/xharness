#!/bin/bash

set -e

version='1.0.0-ci'

# Clean the NuGet cache from the previous 1.0.0-ci version of the tool
# TODO: This might have a better solution: https://github.com/dotnet/xharness/issues/123
echo "Cleaning the NuGet cache from the previous version of the tool..."
cache_dirs=`dotnet nuget locals All -l | cut -d':' -f 2 | tr -d ' '`
while IFS= read -r path; do
    rm -rfv "$path/microsoft.dotnet.xharness.cli/$version"
    rm -rfv "$path/Microsoft.DotNet.XHarness.CLI/$version"
done <<< "$cache_dirs"

set -x

here="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

# TODO - Call: dotnet xharness ios package ...
# For now we download it:
curl -L --output $here/app.zip "https://netcorenativeassets.blob.core.windows.net/resource-packages/external/macos/test-ios-app/System.Numerics.Vectors.Tests.app.zip?sp=r&st=2020-05-04T13:23:20Z&se=2028-05-04T21:23:20Z&spr=https&sv=2019-10-10&sr=b&sig=7sKBDMZrlk%2FA58zlbaUYptb98kK7EacQpmJ9RxlLLrE%3D"
app_name='System.Numerics.Vectors.Tests.app'

tar -xzf app.zip

mkdir $here/tools
cd $here/tools

dotnet new tool-manifest

dotnet tool install --no-cache --version $version --add-source .. Microsoft.DotNet.XHarness.CLI

export XHARNESS_DISABLE_COLORED_OUTPUT=true
export XHARNESS_LOG_WITH_TIMESTAMPS=true

set +e

dotnet xharness ios test \
    --app="$here/$app_name" \
    --output-directory="$HELIX_WORKITEM_UPLOAD_ROOT" \
    --targets=ios-simulator-64 \
    --timeout=600 \
    --launch-timeout=360

result=$?

echo "Remove empty logs"
find "$HELIX_WORKITEM_UPLOAD_ROOT/" -size 0 -print -delete

exit $result
