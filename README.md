# XHarness

This repo contains the code to build the **XHarness dotnet tool**.

## What is XHarness

XHarness is primarily a command line tool that enables running tests on Android and iOS (WatchOS and tvOS are also supported).
It can locate devices/emulators, install a given application, run it and collect results uninstalling it after.
It handles application crashes by collecting crash dumps and supports different types of connection modes (Network, USB cable).
It can output test results in various different formats from text to Xunit/NUnit XML.

The tool can also package given .NET test DLLs (Xunit, NUnit v2/3) into an iOS app bundle that can be run on the device/emulator.

## Running the tool

The tool requires **.NET 3.1.201** and later to be run. It is packaged as a `dotnet tool` command and can be installed using the [dotnet tool CLI](https://docs.microsoft.com/en-us/dotnet/core/tools/).

- The iOS scenarios require you to run the tool on MacOS with Xcode 11.4. **(and pre-installed iOS simulator 13.3 - this will be improved later and specific version won't be needed)**
- Android scenarios are supported on all Linux, MacOS and Windows

To install the tool run:

```bash
dotnet tool install Microsoft.DotNet.XHarness.CLI \
    --global \
    --add-source https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json \
    --version 1.0.0-prerelease.20229.6
```

You can get the specific version from [the dotnet-eng feed](https://dev.azure.com/dnceng/public/_packaging?_a=package&feed=dotnet-eng&view=versions&package=Microsoft.DotNet.XHarness.CLI&protocolType=NuGet) where it is published.
So far we are in preview so omitting the version will fail to locate a stable version of the tool and has to be supplied.

To run the tool, use the `dotnet xharness` command. The tool always expects the platform (`android`/`ios`) as the first argument and has few basic modes:
- `test` - run given application on a device/emulator
- `package` - bundle .NET test DLLs into an application (available on iOS only)
- `state` - print information about the machine and connected devices

Example:

```bash
dotnet xharness android state
```

To list all the possible commands, use the `help` command:

```bash
dotnet xharness help
```

To get help for a specific command or sub-command, run:

```bash
dotnet xharness help ios
dotnet xharness help ios package
```

## Examples

To run an iOS app bundle on a 64bit iPhone Simulator:

```bash
dotnet xharness ios test \
    --app=/path/to/an.app \
    --output-directory=out \
    --targets=ios-simulator-64
```

or the same can be achieved via the shorthand versions of the same options:

```bash
dotnet xharness ios test -a=/path/to/an.app -o=out -t=ios-simulator-64
```

The `out` dir will then contain log files such as these:
```console
iPhone X (iOS 13.3) - created by xharness.log
run-Simulator_iOS64.log
simulator-list-20200430_025916.log
test-ios-simulator-64-20200430_025916.log
test-ios-simulator-64-20200430_025916.xml
```

These files are:
- logs from the Simulator
- logs from the tool itself
- logs from getting the list of available Simulators
- Test results in human readable format
- Test results in XML format (default is xUnit but can be changed via options)

## Test Runners

The repository also contains several TestRunners that are bundled inside of the application and execute the tests.
Currently we support Xunit and NUnit test assemblies but the `Microsoft.DotNet.XHarness.Tests.Runners` supports implementation of custom runner too.

## Contribution

We welcome contributions! Please follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## Filing issues

This repo should contain issues that are tied to the XHarness command line tool and the TestRunners.

For other issues, please use the following repos:

- For .NET runtime and Base Class Library issues, file in the [dotnet/runtime](https://github.com/dotnet/runtime) repo
- For overall .NET SDK issues, file in the [dotnet/sdk](https://github.com/dotnet/sdk) repo

## License

.NET (including the xharness repo) is licensed under the [MIT](LICENSE.TXT) license.
