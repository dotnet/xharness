// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.XHarness.Android;
using Microsoft.DotNet.XHarness.CLI.AndroidHeadless;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.AndroidHeadless;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.AndroidHeadless;

internal class AndroidHeadlessTestCommand : AndroidHeadlessCommand<AndroidHeadlessTestCommandArguments>
{
    private const string ReturnCodeVariableName = "return-code";

    protected override AndroidHeadlessTestCommandArguments Arguments { get; } = new();

    protected override string CommandUsage { get; } = "android-headless test --output-directory=... --test-folder=... --test-command=... [OPTIONS]";

    private const string CommandHelp = "Executes test executable on an Android device, waits up to a given timeout, then copies files off the device and uninstalls the test app";
    protected override string CommandDescription { get; } = @$"
{CommandHelp}
 
Arguments:
";

    public AndroidHeadlessTestCommand() : base("test", false, CommandHelp)
    {
    }

    protected override ExitCode InvokeCommand(ILogger logger)
    {
        if (!File.Exists(Arguments.TestAppPath))
        {
            logger.LogCritical($"Couldn't find {Arguments.TestAppPath}!");
            return ExitCode.PACKAGE_NOT_FOUND;
        }

        if (string.IsNullOrEmpty(Arguments.TestAppCommand))
        {
            logger.LogCritical($"Did not specify a test command");
            return ExitCode.INVALID_ARGUMENTS;
        }

        IEnumerable<string> testRequiredArchitecture = Arguments.DeviceArchitecture.Value;

        logger.LogInformation($"Required architecture: '{string.Join("', '", testRequiredArchitecture)}'");

        var runner = new AdbRunner(logger);

        var exitCode = AndroidHeadlessInstallCommand.InvokeHelper(
            logger: logger,
            testAppPath: Arguments.TestAppPath,
            testRequiredArchitecture: testRequiredArchitecture,
            deviceId: Arguments.DeviceId.Value,
            apiVersion: Arguments.ApiVersion.Value,
            bootTimeoutSeconds: Arguments.LaunchTimeout,
            runner,
            DiagnosticsData);

        if (exitCode == ExitCode.SUCCESS)
        {
            exitCode = AndroidHeadlessRunCommand.InvokeHelper(
                logger: logger,
                testAppCommand: Arguments.TestAppCommand,
                testAppPath: Arguments.TestAppPath,
                testAppArguments: Arguments.TestAppArguments,
                testAppEnvironment: Arguments.TestAppEnvironment,
                outputDirectory: Arguments.OutputDirectory,
                deviceOutputFolder: Arguments.DeviceOutputFolder,
                timeout: Arguments.Timeout,
                expectedExitCode: Arguments.ExpectedExitCode,
                wifi: Arguments.Wifi,
                runner: runner);
        }

        runner.DeleteHeadlessFolder(Arguments.TestAppPath);
        return exitCode;
    }
}
