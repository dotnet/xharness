#!/bin/bash

set -ex

here="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

DOTNET_ROOT=$(dirname "$(which dotnet)")
export DOTNET_ROOT
dotnet tool install --no-cache --tool-path "$here/tools" --version "1.0.0-ci" --add-source "$here" Microsoft.DotNet.XHarness.SimulatorInstaller

sim_installer="$here/tools/simulator-installer"

export XHARNESS_DISABLE_COLORED_OUTPUT=true
export XHARNESS_LOG_WITH_TIMESTAMPS=true

set +ex

echo "Testing simulator download availability"
echo "Getting list of available simulators"

IFS=$'\n'
list=($("$sim_installer" list | grep 'Source:'))

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
    url=$(echo "$random_line" | tr -s ' ' | cut -d ' ' -f 3)
    echo "Testing accessibility of $url..."

    status_code=$(curl --silent --head -o /dev/null --write-out %{http_code} "$url")

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
installed_simulators=($("$sim_installer" list --installed | grep 'Identifier:'))

length="${#installed_simulators[@]}"

if [ "$length" != "0" ]; then
    echo "Found $length installed simulators:"

    simulator_args=""

    for i in "${installed_simulators[@]}"
    do
        pkg_name=$(echo $i | tr -s ' ' | cut -d ' ' -f 3)
        echo "  $pkg_name"
        simulator_args="--simulator=$pkg_name $simulator_args"
    done

    echo ""
    set -x

    eval "$sim_installer" find $simulator_args

    if [ "$?" != 0 ]; then
        echo "Failed to find listed simulators"
        result=1
    else
        echo "Found all listed simulators"
    fi
else
    echo "No additional simulators found (probably only those coming with Xcode)"
fi

exit $result
