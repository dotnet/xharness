#!/usr/bin/env bash

set -euo pipefail

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

output_dir="$scriptroot/artifacts/coverage"
configuration="${1:-Debug}"
format="${2:-cobertura}"

echo "Running tests with code coverage..."
echo "Configuration: $configuration"
echo "Output format: $format"
echo ""

# Clean previous coverage results
rm -rf "$output_dir"
mkdir -p "$output_dir"

# Run tests with coverage
dotnet test "$scriptroot/XHarness.slnx" \
  --configuration "$configuration" \
  --collect:"XPlat Code Coverage" \
  --results-directory "$output_dir" \
  --settings:"$scriptroot/tests/coverlet.runsettings" \
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format="$format"

echo ""
echo "Coverage results generated in: $output_dir"
echo ""
echo "To generate an HTML report, install ReportGenerator:"
echo "  dotnet tool install -g dotnet-reportgenerator-globaltool"
echo ""
echo "Then run:"
echo "  reportgenerator -reports:\"$output_dir/**/coverage.$format.xml\" -targetdir:\"$output_dir/report\" -reporttypes:Html"
echo ""
