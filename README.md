# XHarness

This repo contains the code to build the **XHarness dotnet tool** and a **TestRunner library** that makes running unit tests in mobile platforms easier.

## What is XHarness

XHarness is primarily a command line tool that enables running xUnit like tests on Android, Apple iOS / tvOS / WatchOS / Mac Catalyst and desktop browsers (WASM).
It can
- locate devices/emulators
- install a given application, run it and collect results uninstalling it after,
- perform the operations above as part of one command or separately if need be,
- handle application crashes by collecting crash dumps (symbolicate),
- use different types of connection modes (network, USB cable),
- output test results in various different formats from text to xUnit/NUnit XML
- install Apple Simulator runtimes (different versions of iOS, tvOS...).

## System requirements

The tool requires **.NET 6** or later to be run. It is packaged as a `dotnet tool` command and can be installed using the [dotnet tool CLI](https://docs.microsoft.com/en-us/dotnet/core/tools/).

- The Apple scenarios require you to run the tool on MacOS with full Xcode installation
- Android scenarios are supported on Linux, macOS and Windows systems
- Browsers scenarios are supported on Linux systems

## Try the tool out quickly

If you want to test the tool quickly (MacOS and Linux only), following script will install the required .NET SDK and the XHarness tool locally in the current folder:
```bash
curl -L https://aka.ms/xharness-bootstrap | bash -
```

You can delete the folder after you're done, nothing is installed in your system.

## Installation and usage

To install the latest version of the tool run (in bash):

```bash
dotnet tool install Microsoft.DotNet.XHarness.CLI                                                   \
    --global                                                                                        \
    --add-source https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json \
    --version "1.0.0-prerelease*"
```

Or run (in PowerShell):

```powershell
dotnet tool install Microsoft.DotNet.XHarness.CLI                                                   `
    --global                                                                                        `
    --add-source https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json `
    --version "1.0.0-prerelease*"
```

You can get a specific version from [the dotnet-eng feed](https://dev.azure.com/dnceng/public/_packaging?_a=package&feed=dotnet-eng&view=versions&package=Microsoft.DotNet.XHarness.CLI&protocolType=NuGet) where it is published.
So far, we are in preview so omitting the version will fail to locate a stable version of the tool and fail the installation so a specific version has to be supplied.

To run the tool, use the `xharness` command.
The tool returns one of the exit codes [listed here (ExitCode.cs)](https://github.com/dotnet/xharness/blob/main/src/Microsoft.DotNet.XHarness.Common/CLI/ExitCode.cs).
The tool always expects the platform (`android`/`apple`/`browser`) followed by a command.
To get an up-to-date set of commands, please run `xharness help`.

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
- `XHARNESS_LOG_TEST_START` - log test start messages, useful to diagnose when tests are hanging. Currently only works for WebAssembly
- `XHARNESS_MLAUNCH_PATH` - local path to the mlaunch binary when developing XHarness (when not using as .NET tool)

### Arcade/Helix integration

In case your repository is onboarded into [Arcade](https://github.com/dotnet/arcade) you can use the [Arcade Helix SDK](https://github.com/dotnet/arcade/tree/master/src/Microsoft.DotNet.Helix/Sdk) to run XHarness jobs over Helix. More on how to do that is described [here](https://github.com/dotnet/arcade/blob/master/src/Microsoft.DotNet.Helix/Sdk/tools/xharness-runner/Readme.md).

## Examples

To run an iOS/tvOS app bundle on a 64bit iPhone Simulator:

```bash
xharness apple test           \
    --app=/path/to/an.app     \
    --output-directory=out    \
    --target=ios-simulator-64
```

or the same can be achieved via the shorthand versions of the same options:

```bash
xharness apple test -a=/path/to/an.app -o=out -t=ios-simulator-64
```

The `out` dir will then contain log files such as these:
```console
iPhone X (iOS 13.3) - created by xharness.log   # logs from the Simulator
test-Simulator_iOS64.log                        # logs from the tool itself
test-ios-simulator-64-20200430_025916.xml       # test results in XML format
```

Example for Android apk:

```bash
xharness android test                                    \
    --output-directory=out                               \
    --package-name=net.dot.System.Numerics.Vectors.Tests \
    --app=/path/to/test.apk
```
Output directory will have a file with dump from logcat and a file with tests results.

## Test Runners

The repository also contains several TestRunners which are libraries that can be bundled inside of the application and execute the tests.
The TestRunner detects and executes unit tests inside of the application. It also connects to XHarness over TCP connection from within the running app bundle and reports test run results/state.

There is a library `Microsoft.DotNet.XHarness.DefaultAndroidEntryPoint.Xunit` that provides default logic for Android test app entry point.
It is possible to use `DefaultAndroidEntryPoint` from there for the test app by providing only test result path and test assemblies.
Other parameters can be overrided as well if needed.

Currently we support Xunit and NUnit test assemblies but the `Microsoft.DotNet.XHarness.Tests.Runners` supports implementation of custom runner too.

## Development instructions
When working on XHarness, there are couple of neat hacks that can improve the inner loop.
The repository can either be built using regular .NET, assuming you have new enough version:
```
dotnet build XHarness.sln
```
or you can use the build scripts `build.sh` or `Build.cmd` in repository root which will install the correct .NET SDK into the `.dotnet` folder.
You can then use
```
./.dotnet/dotnet build XHarness.sln
```

You can also use Visual Studio 2019+ and just F5 the `Microsoft.DotNet.XHarness.CLI` project.

### ADB, mlaunch
In order for XHarness to work, you will need ADB (for Android) and mlaunch (for anything Apple).
These are executables that go with the packaged .NET xharness tool.

The easiest way to get these at the moment for development purposes is to build the CLI project and they will be downloaded.
```
dotnet build src/Microsoft.DotNet.XHarness.CLI/Microsoft.DotNet.XHarness.CLI.csproj
```

You can then find these dependencies in `artifacts/obj/Microsoft.DotNet.XHarness.CLI/Debug/net6.0`.

For iOS flows, you can further store the path to mlaunch to an environmental variable `XHARNESS_MLAUNCH_PATH`
```
export XHARNESS_MLAUNCH_PATH='[xharness root]/artifacts/obj/Microsoft.DotNet.XHarness.CLI/Debug/net6.0/mlaunch/bin/mlaunch'
```
and you won't have to specify the `--mlaunch` argument.

### Running E2E tests
In case you want to test your changes in XHarness, you can run E2E tests located in `/tests/integration-tests`. These usually download some pre-built application and send it to our "test cloud" called Helix together with an XHarness version built from your sources. There, XHarness executes the app on a device/simulator.

To run the E2E tests, you can find a script in `tools/` that will build everything and create the cloud job for you:
```
./tools/run-e2e-test.sh Apple/Simulator.Tests.proj
```

## Troubleshooting

Some XHarness commands only work in some scenarios and it's good to know what to expect from the tool.
Some Android/Apple versions also require some workarounds and those are also good to know about.

### My Apple unit tests are not running

For the `apple test` command, XHarness expects the application to contain a `TestRunner` which is a library you can find in this repository.
This library executes unit tests similarly how you would execute them on other platforms.
However, the `TestRunner` from this repository contains more mechanisms that help to work around some issues (mostly in Apple platforms).

The way it works is that XHarness usually sets some [environmental variables](https://github.com/dotnet/xharness/blob/main/src/Microsoft.DotNet.XHarness.iOS.Shared/Execution/EnviromentVariables.cs) for the application and the [`TestRunner` recognizes them](https://github.com/dotnet/xharness/blob/main/src/Microsoft.DotNet.XHarness.TestRunners.Common/ApplicationOptions.cs) and acts upon them.

The workarounds we talk about are for example some TCP connections between the app and XHarness so that we can stream back the test results.

For these reasons, the `test` command won't just work with any app. For those scenarios, use the `apple run` commands.

### iOS/tvOS device runs are timing out

For iOS/tvOS 14+, we have problems detecting when the application exits on the real device (simulators work fine).
The workaround we went with lies in sharing a random string with the application using an [environmental variable `RUN_END_TAG`](https://github.com/dotnet/xharness/blob/main/src/Microsoft.DotNet.XHarness.iOS.Shared/Execution/EnviromentVariables.cs) and expecting the app to output this string at the end of its run.

To turn this workaround on, run XHarness with `--signal-app-end` and make sure your application logs the string it reads from the env variable.
Using the `TestRunner` from this repository will automatically give you this functionality.

## Contribution

We welcome contributions! Please follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## Filing issues

This repo should contain issues that are tied to the XHarness command line tool and the TestRunners.

For other issues, please use the following repos:

- For .NET runtime and Base Class Library issues, file in the [dotnet/runtime](https://github.com/dotnet/runtime) repo
- For overall .NET SDK issues, file in the [dotnet/sdk](https://github.com/dotnet/sdk) repo

## License

.NET (including the xharness repo) is licensed under the [MIT](LICENSE.TXT) license.
