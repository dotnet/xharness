#!/usr/bin/env bash

#
#   Commandlet downloads specific revision of the mlaunch binary from xamarin/macios-binaries repository.
#   This binary is then bundled with XHarness inside the NuGet.
#
#   Revision is cached in a temp dir and re-used for new builds.
#
#   Usage: ./download-mlaunch --commit 343tvfdf3rfqef2dfv3 --target-dir /where-to-install --remove-symbols
#

set -e

# Get current path
source="${BASH_SOURCE[0]}"
# resolve $source until the file is no longer a symlink
while [[ -h "$source" ]]; do
  script_root="$( cd -P "$( dirname "$source" )" && pwd )"
  source="$(readlink "$source")"
  # if $source was a relative symlink, we need to resolve it relative to the path where the
  # symlink file was located
  [[ $source != /* ]] && source="$script_root/$source"
done

script_root="$( cd -P "$( dirname "$source" )" && pwd )"

# shellcheck source=./common/tools.sh
source "$script_root/common/tools.sh"

copy_mlaunch () {
  # Copy mlaunch to the target folder
  cp -Rv "$1" "$2"

  # Clean what we don't need
  rm -rf "$2/lib/mlaunch/mlaunch.app/Contents/MacOS/mlaunch.dSYM"

  if [ "$3" = true ]; then
    echo "Removing debug symbols"
    rm -v "$2"/lib/mlaunch/mlaunch.app/Contents/MonoBundle/*.pdb
  fi
}

commit=''
target_dir="$artifacts_dir/mlaunch"
remove_symbols=false

while (($# > 0)); do
  lowerI="$(echo "$1" | awk '{print tolower($0)}')"
  case $lowerI in
    --commit)
      shift
      commit=$1
      ;;

    --target-dir)
      shift
      target_dir=$1
      ;;

    --remove-symbols)
      remove_symbols=true
      ;;

    --help)
      echo "Usage: download-mlaunch.sh --commit 343tvfdf3rfqef2dfv3 --target-dir /where-to-install"
      exit 0
  esac
  shift
done

if [[ -z $commit ]]; then
  echo "Please specify a git commit ID of the xamarin/macios-binaries repository using the --commit option" 1>&2
  exit 1
fi

echo "Getting mlaunch revision $commit into $target_dir"

tag_file="$target_dir/$commit.tag"

if [ -f "$tag_file" ]; then
  echo "mlaunch is already downloaded"
  exit 0
fi

binaries_repo="$temp_dir/macios-binaries"
tag_file_in_repo="$binaries_repo/$commit.tag"

# Check if the repo was already checked out
if [ -d "$binaries_repo" ]; then
    if [ -f "$tag_file_in_repo" ]; then
        copy_mlaunch "$binaries_repo/mlaunch" "$target_dir" $remove_symbols

        # Tag the version of mlaunch we have
        touch "$tag_file"

        echo "Finished downloading mlaunch from cache"
        exit 0
    fi

    echo "Found cached repository but different version was checked out. Downloading again for $commit..."
    rm -rf "$binaries_repo"
fi

echo "Cloning the xamarin-binaries repository. This might take few minutes.."

mkdir -p "$binaries_repo"

# Shallow-clone the xamarin/macios-binaries repo
cd "$binaries_repo"

git init
git remote add origin https://github.com/xamarin/macios-binaries.git
git config core.sparseCheckout true
echo "mlaunch" >> .git/info/sparse-checkout
git fetch --depth 1 origin $commit
git checkout FETCH_HEAD

copy_mlaunch "$binaries_repo/mlaunch" "$target_dir" $remove_symbols

# Tag the version of mlaunch we have
touch "$tag_file_in_repo"
touch "$tag_file"

echo "Finished installing mlaunch in $target_dir"
