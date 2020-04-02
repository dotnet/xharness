#!/usr/bin/env bash

set -e

# Get current path
source="${BASH_SOURCE[0]}"
# resolve $source until the file is no longer a symlink
while [[ -h "$source" ]]; do
  scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"
  source="$(readlink "$source")"
  # if $source was a relative symlink, we need to resolve it relative to the path where the
  # symlink file was located
  [[ $source != /* ]] && source="$scriptroot/$source"
done
scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"

. "$scriptroot/common/tools.sh"

# Parse arguments
commit='master'
while [[ $# > 0 ]]; do
  opt="$(echo "$1" | awk '{print tolower($0)}')"
  case "$opt" in
    -commit|-c)
      shift
      commit="$1"
      ;;
    *)
      echo "Invalid argument: $1"
      exit 1
      ;;
  esac
  shift
done

if [[ -z $commit ]]; then
  echo "Please use '-commit [ID]' to specify git commit ID of the xamarin/macios-binaries repository to checkout to"
  exit 1
fi

target_dir="$artifacts_dir/mlaunch"
tag_file="$target_dir/.tag-$commit"

if [ -f "$tag_file" ]; then
  echo "mlaunch version $commit is already installed"
  exit 0
fi

binaries_repo="$temp_dir/macios-binaries"

rm -rf "$binaries_repo"
mkdir -p "$binaries_repo"

# Shallow-clone the macios-binaries repo
cd "$binaries_repo"
git init
git remote add origin https://github.com/xamarin/macios-binaries.git
git config core.sparseCheckout true
echo "mlaunch" >> .git/info/sparse-checkout
git fetch --depth 1 origin $commit
git checkout FETCH_HEAD

# Copy mlaunch to the artifacts folder
mv -v "$binaries_repo/mlaunch" "$target_dir"

# Tag the version of mlaunch we have
touch "$target_dir/tag-$commit"

echo "Finished installing mlaunch in $target_dir"
