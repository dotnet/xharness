// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Android;
using Microsoft.DotNet.XHarness.CLI.Android;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Android;

internal class AndroidUninstallCommand : AndroidCommand<AndroidUninstallCommandArguments>
{
    protected override AndroidUninstallCommandArguments Arguments { get; } = new();

    protected override string CommandUsage { get; } = "android uninstall --package-name=... [OPTIONS]";

    private const string CommandHelp = "Uninstall an .apk from an Android device";
    protected override string CommandDescription { get; } = @$"
{CommandHelp}
 
Arguments:
";

    public AndroidUninstallCommand() : base("uninstall", false, CommandHelp)
    {
    }

    protected override Task<ExitCode> InvokeInternal(ILogger logger)
    {
        logger.LogDebug($"Android Uninstall command called: App = {Arguments.PackageName}{Environment.NewLine}");

        // Package Name is not guaranteed to match file name, so it needs to be mandatory.
        string apkPackageName = Arguments.PackageName;

        var runner = new AdbRunner(logger);

        try
        {
            using (logger.BeginScope("Find device where to uninstall APK"))
            {
                // Make sure the adb server is started
                runner.StartAdbServer();

                string? deviceId = Arguments.DeviceId;

                if (string.IsNullOrEmpty(deviceId))
                {
                    // trying to find out if there is only one device with the app installed
                    deviceId = runner.GetUniqueDeviceToUse(logger, "package:" + apkPackageName, "app");
                    if (string.IsNullOrEmpty(deviceId))
                    {
                        return Task.FromResult(ExitCode.ADB_DEVICE_ENUMERATION_FAILURE);
                    }
                }

                runner.SetActiveDevice(deviceId);
                DiagnosticsData.TargetOS = "API " + runner.APIVersion;
                DiagnosticsData.Device = deviceId;

                logger.LogDebug($"Working with {runner.GetAdbVersion()}");

                runner.UninstallApk(apkPackageName);
                return Task.FromResult(ExitCode.SUCCESS);
            }
        }
        catch (Exception toLog)
        {
            logger.LogCritical(toLog, $"Failure to uninstall test package: {toLog.Message}");
        }

        return Task.FromResult(ExitCode.GENERAL_FAILURE);
    }
}
