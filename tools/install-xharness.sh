#!/bin/bash

### This script is a quick way to install and test XHarness on any POSIX system.
### It installs the .NET SDK and the XHarness tool, everything in a local folder which can be deleted afterwards.
###
### Use the following command to run this script from anywhere:
### curl -L https://aka.ms/get-xharness | bash -

set -e

version='*'

while [[ $# -gt 0 ]]; do
    opt="$(echo "$1" | awk '{print tolower($0)}')"
    case "$opt" in
      --version)
        version=$2
        shift
        ;;
      *)
        echo "Invalid argument: $1"
        exit 1
        ;;
    esac
    shift
done

xharness_version="10.0.0-prerelease.$version"

here=$(pwd)
dotnet_install="$here/dotnet-install.sh"

echo "Getting dotnet-install.sh.."
curl -L https://builds.dotnet.microsoft.com/dotnet/scripts/v1/dotnet-install.sh -o "$dotnet_install"
chmod u+x "$dotnet_install"
dotnet_dir="$here/.dotnet"

printf "Installing .NET SDK locally to \033[0;33m%s\033[0m..\n" "$dotnet_dir"
$dotnet_install --install-dir "$dotnet_dir" --channel 8.0
echo 'dotnet installed'

export DOTNET_ROOT="$here/.dotnet"

printf "Installing XHarness.CLI locally to \033[0;33m%s\033[0m..\n" "$here"
./.dotnet/dotnet tool install --tool-path . --version "$xharness_version" --add-source https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json Microsoft.DotNet.XHarness.CLI
echo "XHarness.CLI installed"

echo 'Run following command:'
printf "\033[0;33mexport DOTNET_ROOT=\"%s\"\033[0m\n\n" "$here/.dotnet"
echo 'Then run XHarness using:'
printf "\033[0;33m./xharness help\033[0m\n\n"
