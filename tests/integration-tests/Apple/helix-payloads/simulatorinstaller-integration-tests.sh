#!/bin/bash

echo "Testing simulator download availability"
echo "Getting list of available simulators"

IFS=$'\n'
list=($(dotnet "$XHARNESS_CLI_PATH" apple simulators list | grep 'Source:'))

length="${#list[@]}"

echo "Found $length simulators"
echo ""

if [ ! "$length" -gt 0 ]; then
    echo "Couldn't list available simulators" 1>&2
    exit 1
fi

result=0

echo "Testing installed simulators and the find command"

IFS=$'\n'
installed_simulators=($(dotnet "$XHARNESS_CLI_PATH" apple simulators list --installed | grep 'Identifier:'))

length="${#installed_simulators[@]}"

if [ "$length" != "0" ]; then
    echo "Found $length installed simulators:"

    simulator_args=""

    for i in "${installed_simulators[@]}"
    do
        pkg_name=$(echo $i | tr -s ' ' | cut -d ' ' -f 3)
        echo "  $pkg_name"
        simulator_args="$simulator_args $pkg_name"
    done

    echo ""
    set -x

    eval dotnet "$XHARNESS_CLI_PATH" apple simulators find $simulator_args

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
