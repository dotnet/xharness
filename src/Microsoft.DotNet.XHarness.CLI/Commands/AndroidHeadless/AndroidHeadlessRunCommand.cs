// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.DotNet.XHarness.Android;
using Microsoft.DotNet.XHarness.Android.Execution;
using Microsoft.DotNet.XHarness.CLI.Android;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.AndroidHeadless;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.AndroidHeadless;

internal class AndroidHeadlessRunCommand : AndroidCommand<AndroidHeadlessRunCommandArguments>
{
    protected override AndroidHeadlessRunCommandArguments Arguments { get; } = new();

    protected override string CommandUsage { get; } = "android-headless run --output-directory=... --test-assembly=... [OPTIONS]";

    private const string CommandHelp = "Run tests using an already installed executable on an Android device";
    protected override string CommandDescription { get; } = @$"
{CommandHelp}
 
Arguments:
";

    public AndroidHeadlessRunCommand() : base("run", false, CommandHelp)
    {
    }

    protected override ExitCode InvokeCommand(ILogger logger)
    {
        var runner = new AdbRunner(logger);

        // Make sure the adb server is started
        runner.StartAdbServer();

        var device = string.IsNullOrEmpty(Arguments.DeviceId.Value)
            ? runner.GetSingleDevice(loadArchitecture: true, loadApiVersion: true, requiredInstalledApp: "filename:" + Arguments.TestPath)
            : runner.GetSingleDevice(loadArchitecture: true, loadApiVersion: true, requiredDeviceId: Arguments.DeviceId.Value);

        if (device is null)
        {
            return ExitCode.DEVICE_NOT_FOUND;
        }

        DiagnosticsData.CaptureDeviceInfo(device);

        runner.TimeToWaitForBootCompletion = Arguments.LaunchTimeout;

        // Wait till at least device(s) are ready
        runner.WaitForDevice();

        return InvokeHelper(
            logger,
            Arguments.TestPath,
            Arguments.RuntimePath,
            Arguments.TestAssembly,
            Arguments.TestScript,
            Arguments.OutputDirectory,
            Arguments.Timeout,
            Arguments.ExpectedExitCode,
            Arguments.Wifi,
            runner);
    }

    public static ExitCode InvokeHelper(
        ILogger logger,
        string testPath,
        string runtimePath,
        string testAssembly,
        string testScript,
        string outputDirectory,
        TimeSpan timeout,
        int expectedExitCode,
        WifiStatus wifi,
        AdbRunner runner)
    {
        logger.LogDebug($"Working with API {runner.GetAdbVersion()}");

        // Empty log as we'll be uploading the full logcat for this execution
        runner.ClearAdbLog();

        if (wifi != WifiStatus.Unknown)
        {
            runner.EnableWifi(wifi == WifiStatus.Enable);
        }

        // No class name = default Instrumentation
        ProcessExecutionResults? result = runner.RunHeadlessCommand(
            testPath,
            runtimePath,
            testAssembly,
            testScript,
            timeout);

        bool processCrashed = false;
        bool failurePullingFiles = false;

        using (logger.BeginScope("Post-test copy and cleanup"))
        {
            if (result.ExitCode == (int)ExitCode.SUCCESS)
            {
                var testResultPath = Path.Combine(AdbRunner.GlobalReadWriteDirectory, new DirectoryInfo(testPath).Name, "testResults.xml");

                logger.LogInformation($"Trying to pull results file {testResultPath}");
                runner.HeadlessPullFiles(testResultPath, outputDirectory);
            }
            else
            {
                logger.LogError($"Non-success exit code: {result.ExitCode}, expected: {expectedExitCode}");
                return ExitCode.TESTS_FAILED;
            }

            runner.DumpAdbLog(Path.Combine(outputDirectory, $"adb-logcat-{testAssembly}-default.log"));

            if (processCrashed)
            {
                runner.DumpBugReport(Path.Combine(outputDirectory, $"adb-bugreport-{testAssembly}"));
            }
        }

        if (processCrashed)
        {
            return ExitCode.APP_CRASH;
        }

        if (failurePullingFiles)
        {
            logger.LogError($"Hit errors pulling files from the device (see log for details.)");
            return ExitCode.DEVICE_FILE_COPY_FAILURE;
        }

        return ExitCode.SUCCESS;
    }
}
