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
    sudo rm -vrf "$path/microsoft.dotnet.xharness.simulatorinstaller/$version"
    sudo rm -vrf "$path/Microsoft.DotNet.XHarness.SimulatorInstaller/$version"
done <<< "$cache_dirs"

set -x

here="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
cd $here

mkdir $here/tools
cd $here/tools

dotnet new tool-manifest

dotnet tool install --no-cache --version $version --add-source .. Microsoft.DotNet.XHarness.SimulatorInstaller

export XHARNESS_DISABLE_COLORED_OUTPUT=true
export XHARNESS_LOG_WITH_TIMESTAMPS=true

dotnet tool restore --no-cache

set +ex

echo "Testing simulator download availability"
echo "Getting list of available simulators"

IFS=$'\n'
list=($(dotnet simulator-installer list | grep 'Source:'))

length="${#list[@]}"

echo "Found $length simulators"
echo ""

if [ ! "$length" -gt 0 ]; then
    echo "Couldn't list available simulators" 1>&2
    exit 1
fi

result=0

# Test 3 random URLs
for (( i=0; i<3; i++ )); do
    random_line=${list[$RANDOM % ${#list[@]}]}
    url=`echo $random_line | tr -s ' ' | cut -d ' ' -f 3`
    echo "Testing accessibility of $url..."

    status_code=`curl --silent --head -o /dev/null --write-out %{http_code} "$url"`

    echo "Result: $status_code"

    if [ "$status_code" == "200" ]; then
        echo "Test succeeded"
    else
        echo "Failed loading URL"
        result=1
    fi

    echo ""
done

echo "Testing installed simulators and the find command"

IFS=$'\n'
installed_simulators=($(dotnet simulator-installer list --installed | grep 'Identifier:'))

length="${#installed_simulators[@]}"

if [ ! "$length" -gt 0 ]; then
    echo "Found $length installed simulators:"

    simulator_args=""

    for i in "${installed_simulators[@]}"
    do
        pkg_name=`echo $i | tr -s ' ' | cut -d ' ' -f 3`
        echo "  $pkg_name"
        simulator_args="--simulator=$pkg_name $simulator_args"
    done

    echo ""
    set -x

    eval dotnet simulator-installer find $simulator_args

    if [ "$?" != 0 ]; then
        echo "Failed to find listed simulators"
        result=1
    else
        echo "Found all listed simulators"
    fi
else
    echo "No additional simulators found (probably only those coming with Xcode)"
fi

cd $here
rm -rf $here/tools

exit $result
