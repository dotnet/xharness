#!/bin/bash

### This script is a quick way to install and test XHarness on any machine (intended for MacOS)
### It installs the .NET Core 3.1 SDK and the XHarness tool
### It then downloads a sample app and writes out a command to run this app
### Everything is installed locally in the current folder and can be removed when you're done
###
### Use the following command to run this script from anywhere:
### curl -L https://aka.ms/xharness-bootstrap | bash /dev/stdin

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

xharness_version="1.0.0-prerelease.$version"

here=$(pwd)
dotnet_install="$here/dotnet-install.sh"

echo "Getting dotnet-install.sh.."
curl -L https://dot.net/v1/dotnet-install.sh -o "$dotnet_install"
chmod u+x "$dotnet_install"
dotnet_dir="$here/.dotnet"

printf "Installing dotnet to \033[0;36m%s\033[0m.." "$dotnet_dir"
$dotnet_install --install-dir "$dotnet_dir" --channel "3.1"
echo 'dotnet installed'

export DOTNET_ROOT="$here/.dotnet"

printf "Installing XHarness.CLI to \033[0;36m./xharness\033[0m.."
./.dotnet/dotnet tool install --tool-path xharness --version "$xharness_version" --add-source https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json Microsoft.DotNet.XHarness.CLI || true
echo "XHarness.CLI installed"

printf "Run XHarness using:\n\033[0;36m./xharness/xharness ios test --app=[path to iOS .app bundle] --output-directory=o --targets=ios-simulator-64\033[0m\n\n"
