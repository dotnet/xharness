using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.XHarness.Android;
using Microsoft.DotNet.XHarness.CLI.Android;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Android;

internal class AndroidInstallCommand : AndroidCommand<AndroidInstallCommandArguments>
{
    protected override AndroidInstallCommandArguments Arguments { get; } = new();

    protected override string CommandUsage { get; } = "android install --package-name=... --app=... [OPTIONS]";

    private const string CommandHelp = "Install an .apk on an Android device without running it";
    protected override string CommandDescription { get; } = @$"
{CommandHelp}
 
Arguments:
";

    public AndroidInstallCommand() : base("install", false, CommandHelp)
    {
    }

    protected override ExitCode InvokeCommand(ILogger logger)
    {
        if (!File.Exists(Arguments.AppPackagePath))
        {
            logger.LogCritical($"Couldn't find {Arguments.AppPackagePath}!");
            return ExitCode.PACKAGE_NOT_FOUND;
        }

        var runner = new AdbRunner(logger);

        List<string> apkRequiredArchitecture = new();

        if (string.IsNullOrEmpty(Arguments.DeviceId))
        {
            // trying to choose suitable device
            if (Arguments.DeviceArchitecture.Value.Any())
            {
                apkRequiredArchitecture = Arguments.DeviceArchitecture.Value.ToList();
                logger.LogInformation($"Will attempt to run device on specified architecture: '{string.Join("', '", apkRequiredArchitecture)}'");
            }
            else
            {
                apkRequiredArchitecture = ApkHelper.GetApkSupportedArchitectures(Arguments.AppPackagePath);
                logger.LogInformation($"Will attempt to run device on detected architecture: '{string.Join("', '", apkRequiredArchitecture)}'");
            }
        }

        return InvokeHelper(
            logger: logger,
            apkPackageName: Arguments.PackageName,
            appPackagePath: Arguments.AppPackagePath,
            apkRequiredArchitecture: apkRequiredArchitecture,
            deviceId: Arguments.DeviceId,
            apiVersion: Arguments.ApiVersion.Value,
            bootTimeoutSeconds: Arguments.LaunchTimeout,
            runner: runner,
            DiagnosticsData);
    }

    public static ExitCode InvokeHelper(
        ILogger logger,
        string apkPackageName,
        string appPackagePath,
        IEnumerable<string> apkRequiredArchitecture,
        string? deviceId,
        int? apiVersion,
        TimeSpan bootTimeoutSeconds,
        AdbRunner runner,
        IDiagnosticsData diagnosticsData)
    {
        using (logger.BeginScope("Initialization and setup of APK on device"))
        {
            // Make sure the adb server is started
            runner.StartAdbServer();

            AndroidDevice? device = runner.GetDevice(
                loadArchitecture: true,
                loadApiVersion: true,
                deviceId,
                apiVersion,
                apkRequiredArchitecture);

            if (device is null)
            {
                throw new NoDeviceFoundException($"Failed to find compatible device: {string.Join(", ", apkRequiredArchitecture)}");
            }

            diagnosticsData.CaptureDeviceInfo(device);

            runner.TimeToWaitForBootCompletion = bootTimeoutSeconds;

            // Wait till at least device(s) are ready
            runner.WaitForDevice();

            logger.LogDebug($"Working with {device.DeviceSerial} (API {device.ApiVersion})");

            // If anything changed about the app, Install will fail; uninstall it first.
            // (we'll ignore if it's not present)
            // This is where mismatched architecture APKs fail.
            runner.UninstallApk(apkPackageName);
            if (runner.InstallApk(appPackagePath) != 0)
            {
                logger.LogCritical("Install failure: Test command cannot continue");
                runner.UninstallApk(apkPackageName);
                return ExitCode.PACKAGE_INSTALLATION_FAILURE;
            }

            runner.KillApk(apkPackageName);
        }
        return ExitCode.SUCCESS;
    }
}
