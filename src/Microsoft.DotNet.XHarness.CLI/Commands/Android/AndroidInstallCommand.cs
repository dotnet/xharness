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
        var emulatorManager = new EmulatorManager(logger, runner);

        return InvokeHelper(
            logger: logger,
            apkPackageName: Arguments.PackageName,
            appPackagePath: Arguments.AppPackagePath,
            requestedArchitectures: Arguments.DeviceArchitecture.Value.ToList(),
            deviceId: Arguments.DeviceId,
            apiVersion: Arguments.ApiVersion.Value,
            bootTimeoutSeconds: Arguments.LaunchTimeout,
            resetEmulator: Arguments.ResetEmulator,
            runner: runner,
            emulatorManager: emulatorManager,
            DiagnosticsData);
    }

    public static ExitCode InvokeHelper(
        ILogger logger,
        string apkPackageName,
        string appPackagePath,
        IReadOnlyCollection<string> requestedArchitectures,
        string? deviceId,
        int? apiVersion,
        TimeSpan bootTimeoutSeconds,
        bool resetEmulator,
        AdbRunner runner,
        EmulatorManager emulatorManager,
        IDiagnosticsData diagnosticsData)
    {
        using (logger.BeginScope("Initialization and setup of APK on device"))
        {
            IReadOnlyCollection<string> requiredArchitectures;
            var apkSupportedArchitectures = ApkHelper.GetApkSupportedArchitectures(appPackagePath);

            if (requestedArchitectures.Any())
            {
                if (!apkSupportedArchitectures.Intersect(requestedArchitectures).Any())
                {
                    logger.LogError("The APK at {appPackagePath} supports {apkSupportedArchitectures} architectures " +
                        "which does not match any of the specified architectures ({requestedArchitectures})",
                        appPackagePath,
                        string.Join(", ", apkSupportedArchitectures),
                        string.Join(", ", requestedArchitectures));
                    return ExitCode.INVALID_ARGUMENTS;
                }

                requiredArchitectures = requestedArchitectures;
            }
            else
            {
                requiredArchitectures = apkSupportedArchitectures;
            }

            logger.LogInformation("Will attempt to find device supporting architectures: '{requiredArchitectures}'",
                string.Join("', '", requiredArchitectures));

            // Make sure the adb server is started
            runner.StartAdbServer();

            // Always attempt to find or start emulator (matching iOS behavior)
            var device = runner.GetDeviceOrStartEmulator(
                emulatorManager: emulatorManager,
                startEmulatorIfNeeded: true,
                wipeEmulatorData: resetEmulator,
                loadArchitecture: true,
                loadApiVersion: true,
                requiredDeviceId: deviceId,
                requiredApiVersion: apiVersion,
                requiredArchitectures: requiredArchitectures);

            if (device is null)
            {
                throw new NoDeviceFoundException($"Failed to find compatible device: {string.Join(", ", requiredArchitectures)}");
            }

            diagnosticsData.CaptureDeviceInfo(device);

            runner.TimeToWaitForBootCompletion = bootTimeoutSeconds;

            // Wait till at least device(s) are ready
            if (!runner.WaitForDevice())
            {
                return ExitCode.DEVICE_NOT_FOUND;
            }

            logger.LogDebug($"Working with {device.DeviceSerial} (API {device.ApiVersion})");

            // TMP: set verifier_verify_adb_installs to 1
            runner.RunAdbCommand(["shell", "settings", "put", "global", "verifier_verify_adb_installs", "0"]);
            runner.RunAdbCommand(["shell", "settings", "put", "global", "package_verifier_enable", "0"]);
            runner.CheckPackageVerificationSettings();

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
