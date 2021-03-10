# XHarness

This repo contains the code to build the **XHarness dotnet tool**.

## What is XHarness

XHarness is primarily a command line tool that enables running xUnit like tests on Android, Apple iOS / tvOS / WatchOS and desktop Browsers.
It can locate devices/emulators, install a given application, run it and collect results uninstalling it after.
It handles application crashes by collecting crash dumps and supports different types of connection modes (Network, USB cable).
It can output test results in various different formats from text to xUnit/NUnit XML.

## System requirements

The tool requires **.NET Core 3.1.201** or later to be run. It is packaged as a `dotnet tool` command and can be installed using the [dotnet tool CLI](https://docs.microsoft.com/en-us/dotnet/core/tools/).

- The Apple scenarios require you to run the tool on MacOS with full Xcode installation
- Android scenarios are supported on Linux, macOS and Windows systems
- Browsers scenarios are supported on Linux systems

## Try the tool out quickly

If you want to test the tool quickly (MacOS and Linux only), following script will install the required .NET SDK and the XHarness tool locally in the current folder:
```
curl -L https://aka.ms/xharness-bootstrap | bash -
```

You can delete the folder after you're done, nothing is installed in your system.

## Installation and usage

To install the latest version of the tool run (in bash):

```bash
dotnet tool install Microsoft.DotNet.XHarness.CLI \
    --global \
    --add-source https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json \
    --version "1.0.0-prerelease*"
```

Or run (in PowerShell):

```powershell
dotnet tool install Microsoft.DotNet.XHarness.CLI `
    --global `
    --add-source https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json `
    --version "1.0.0-prerelease*"
```

You can get a specific version from [the dotnet-eng feed](https://dev.azure.com/dnceng/public/_packaging?_a=package&feed=dotnet-eng&view=versions&package=Microsoft.DotNet.XHarness.CLI&protocolType=NuGet) where it is published.
So far we are in preview so omitting the version will fail to locate a stable version of the tool and it has to be supplied.

To run the tool, use the `xharness` command. The tool always expects the platform (`android`/`apple`/`browser`) followed by a command. To get an up-to-date set of commands, please run `xharness help`.

> Applications run via the `apple test` command require a TestRunner inside of the iOS/tvOS app bundle to work properly.
The `apple run` command, on the other hand, doesn't expect the TestRunner and only runs the application and tries to detect the exit code. Detection of exit code might not work across different iOS versions reliably.
>
> **\*** See the [Test Runners section](#test-runners).

Example:

```bash
xharness android state
```

To list all the possible commands, use the `help` command:

```bash
xharness help
```

To get help for a specific command or sub-command, run:

```bash
xharness help apple
xharness help apple test
```

### Other settings

There are other settings which can be controlled via **environmental variables** and are primarily meant for build pipeline scenarios:

- `XHARNESS_DISABLE_COLORED_OUTPUT` - disable colored logging so that control characters are not making the logs hard to read
- `XHARNESS_LOG_WITH_TIMESTAMPS` - enable timestamps for logging
- `XHARNESS_LOG_TEST_START` - log test start messages, useful to diagnose when tests are hanging. Currently only works for WebAssembly.

### Arcade/Helix integration

In case your repository is onboarded into [Arcade](https://github.com/dotnet/arcade) you can use the [Arcade Helix SDK](https://github.com/dotnet/arcade/tree/master/src/Microsoft.DotNet.Helix/Sdk) to run XHarness jobs over Helix. More on how to do that is described [here](https://github.com/dotnet/arcade/blob/master/src/Microsoft.DotNet.Helix/Sdk/tools/xharness-runner/Readme.md).

## Examples

To run an iOS/tvOS app bundle on a 64bit iPhone Simulator:

```bash
xharness apple test \
    --app=/path/to/an.app \
    --output-directory=out \
    --targets=ios-simulator-64
```

or the same can be achieved via the shorthand versions of the same options:

```bash
xharness apple test -a=/path/to/an.app -o=out -t=ios-simulator-64
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

Example for Android apk:

```bash
xharness android test --output-directory=out --package-name=net.dot.System.Numerics.Vectors.Tests --app=/path/to/test.apk
```
Output directory will have a file with dump from logcat and a file with tests results.

## Test Runners

The repository also contains several TestRunners which are libraries that can be bundled inside of the application and execute the tests.
The TestRunner detects and executes unit tests inside of the application. It also connects to XHarness over TCP connection from within the running app bundle and reports test run results/state.

There is a library `Microsoft.DotNet.XHarness.DefaultAndroidEntryPoint.Xunit` that provides default logic for Android test app entry point.
It is possible to use `DefaultAndroidEntryPoint` from there for the test app by providing only test result path and test assemblies.
Other parameters can be overrided as well if needed.

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
