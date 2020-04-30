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

- The iOS scenarios require you to run the tool on MacOS with Xcode 11.4.
- Android scenarios are supported on all Linux, MacOS and Windows

To install the tool run:

```console
dotnet tool install --global --add-source https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json Microsoft.DotNet.XHarness.CLI --version 1.0.0-prerelease.20229.6
```

You can get the specific version from [the dotnet-eng feed](https://dev.azure.com/dnceng/public/_packaging?_a=package&feed=dotnet-eng&view=versions&package=Microsoft.DotNet.XHarness.CLI&protocolType=NuGet) where it is published.
So far we are in preview so omitting the version will fail to locate a stable version of the tool and has to be supplied.

To run the tool, use the `dotnet xharness` command. The tool always expects the platform (`android`/`ios`) as the first argument and has few basic modes:
- `test` - run given application on a device/emulator
- `package` - bundle .NET test DLLs into an application (available on iOS only)
- `state` - print information about the machine and connected devices

Example:

```console
dotnet xharness android state
```

To list all the possible commands, use the `help` command:

```console
dotnet xharness help
```

To get help for a sub-command command:

```console
dotnet xharness ios test help
```

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
