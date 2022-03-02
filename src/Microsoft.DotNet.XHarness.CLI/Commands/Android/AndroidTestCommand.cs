// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.XHarness.Android;
using Microsoft.DotNet.XHarness.CLI.Android;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Android;

internal class AndroidTestCommand : AndroidCommand<AndroidTestCommandArguments>
{
    private const string ReturnCodeVariableName = "return-code";

    protected override AndroidTestCommandArguments Arguments { get; } = new();

    protected override string CommandUsage { get; } = "android test --output-directory=... --package-name=... --app=... [OPTIONS]";

    private const string CommandHelp = "Executes test .apk on an Android device, waits up to a given timeout, then copies files off the device and uninstalls the test app";
    protected override string CommandDescription { get; } = @$"
{CommandHelp}

APKs can communicate status back to XHarness using the parameters:

Required:
{ReturnCodeVariableName} - Exit code for instrumentation. Necessary because a crashing instrumentation may be indistinguishable from a passing one from exit codes.
 
Arguments:
";

    public AndroidTestCommand() : base("test", false, CommandHelp)
    {
    }

    protected override ExitCode InvokeCommand(ILogger logger)
    {
        if (!File.Exists(Arguments.AppPackagePath))
        {
            logger.LogCritical($"Couldn't find {Arguments.AppPackagePath}!");
            return ExitCode.PACKAGE_NOT_FOUND;
        }

        IEnumerable<string> apkRequiredArchitecture = Arguments.DeviceArchitecture.Value.Any()
            ? Arguments.DeviceArchitecture.Value
            : ApkHelper.GetApkSupportedArchitectures(Arguments.AppPackagePath);

        logger.LogInformation($"Required architecture: '{string.Join("', '", apkRequiredArchitecture)}'");

        var runner = new AdbRunner(logger);

        var exitCode = AndroidInstallCommand.InvokeHelper(
            logger: logger,
            apkPackageName: Arguments.PackageName,
            appPackagePath: Arguments.AppPackagePath,
            apkRequiredArchitecture: apkRequiredArchitecture,
            deviceId: Arguments.DeviceId.Value,
            apiVersion: Arguments.ApiVersion.Value,
            bootTimeoutSeconds: Arguments.LaunchTimeout,
            runner,
            DiagnosticsData);

        if (exitCode == ExitCode.SUCCESS)
        {
            exitCode = AndroidRunCommand.InvokeHelper(
                logger: logger,
                apkPackageName: Arguments.PackageName,
                instrumentationName: Arguments.InstrumentationName,
                instrumentationArguments: Arguments.InstrumentationArguments,
                outputDirectory: Arguments.OutputDirectory,
                deviceOutputFolder: Arguments.DeviceOutputFolder,
                timeout: Arguments.Timeout,
                expectedExitCode: Arguments.ExpectedExitCode,
                wifi: Arguments.Wifi,
                runner: runner);
        }

        runner.UninstallApk(Arguments.PackageName);
        return exitCode;
    }
}
