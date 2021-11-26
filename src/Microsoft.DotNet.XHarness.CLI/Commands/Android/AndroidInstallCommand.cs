using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

    protected override Task<ExitCode> InvokeInternal(ILogger logger)
    {
        logger.LogDebug($"Android Install command called: App = {Arguments.AppPackagePath}");
        logger.LogDebug($"Timeout = {Arguments.Timeout.Value.TotalSeconds} seconds.");

        if (!File.Exists(Arguments.AppPackagePath))
        {
            logger.LogCritical($"Couldn't find {Arguments.AppPackagePath}!");
            return Task.FromResult(ExitCode.PACKAGE_NOT_FOUND);
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

        // Package Name is not guaranteed to match file name, so it needs to be mandatory.
        try
        {
            return Task.FromResult(InvokeHelper(
            logger: logger,
            apkPackageName: Arguments.PackageName,
            appPackagePath: Arguments.AppPackagePath,
            apkRequiredArchitecture: apkRequiredArchitecture,
            deviceId: Arguments.DeviceId,
            bootTimeoutSeconds: Arguments.LaunchTimeout,
            runner: runner,
            DiagnosticsData));
        }
        catch (NoDeviceFoundException noDevice)
        {
            logger.LogCritical(noDevice, noDevice.Message);
            return Task.FromResult(ExitCode.ADB_DEVICE_ENUMERATION_FAILURE);
        }
        catch (Exception toLog)
        {
            logger.LogCritical(toLog, toLog.Message);
        }
        return Task.FromResult(ExitCode.GENERAL_FAILURE);
    }

    public static ExitCode InvokeHelper(
        ILogger logger,
        string apkPackageName,
        string appPackagePath,
        IEnumerable<string> apkRequiredArchitecture,
        string? deviceId,
        TimeSpan bootTimeoutSeconds,
        AdbRunner runner,
        IDiagnosticsData diagnosticsData)
    {
        using (logger.BeginScope("Initialization and setup of APK on device"))
        {
            // Make sure the adb server is started
            runner.StartAdbServer();

            // if call via install command device id must be set
            // otherwise - from test command - apkRequiredArchitecture was set by user or .apk architecture
            deviceId ??= apkRequiredArchitecture.Any()
                ? runner.GetDeviceToUse(logger, apkRequiredArchitecture, "architecture")
                : throw new ArgumentException("Required architecture not specified");

            if (deviceId == null)
            {
                throw new NoDeviceFoundException($"Failed to find compatible device: {string.Join(", ", apkRequiredArchitecture)}");
            }

            runner.SetActiveDevice(deviceId);

            FillDiagnosticData(diagnosticsData, deviceId, runner.APIVersion, apkRequiredArchitecture);

            runner.TimeToWaitForBootCompletion = bootTimeoutSeconds;

            // Wait till at least device(s) are ready
            runner.WaitForDevice();

            logger.LogDebug($"Working with {runner.GetAdbVersion()}");

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
