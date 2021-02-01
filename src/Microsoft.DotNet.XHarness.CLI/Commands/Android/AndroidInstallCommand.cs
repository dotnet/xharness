using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Android;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.Common.CLI.Commands;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Android
{
    internal class AndroidInstallCommand : XHarnessCommand
    {

        private readonly AndroidInstallCommandArguments _arguments = new AndroidInstallCommandArguments();

        protected override XHarnessCommandArguments Arguments => _arguments;

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
            logger.LogDebug($"Android Install command called: App = {_arguments.AppPackagePath}");
            logger.LogDebug($"Timeout = {_arguments.Timeout.TotalSeconds} seconds.");

            if (!File.Exists(_arguments.AppPackagePath))
            {
                logger.LogCritical($"Couldn't find {_arguments.AppPackagePath}!");
                return Task.FromResult(ExitCode.PACKAGE_NOT_FOUND);
            }

            // Assumption: APKs we test will only have one arch for now
            string apkRequiredArchitecture;

            if (string.IsNullOrEmpty(_arguments.DeviceArchitecture))
            {
                apkRequiredArchitecture = ApkHelper.GetApkSupportedArchitectures(_arguments.AppPackagePath).First();
                logger.LogInformation($"Will attempt to run device on detected architecture: '{apkRequiredArchitecture}'");
            }
            else
            {
                apkRequiredArchitecture = _arguments.DeviceArchitecture;
                logger.LogInformation($"Will attempt to run device on specified architecture: '{apkRequiredArchitecture}'");
            }

            var runner = new AdbRunner(logger);

            // Package Name is not guaranteed to match file name, so it needs to be mandatory.
            return Task.FromResult(InvokeHelper(logger, _arguments.PackageName, _arguments.AppPackagePath, apkRequiredArchitecture, _arguments.DeviceId, runner));
        }

        public ExitCode InvokeHelper(ILogger logger, string apkPackageName, string appPackagePath, string apkRequiredArchitecture, string? deviceId, AdbRunner runner)
        {
            try
            {
                using (logger.BeginScope("Initialization and setup of APK on device"))
                {
                    // Make sure the adb server is started
                    runner.StartAdbServer();

                    // enumerate the devices attached and their architectures
                    // Tell ADB to only use that one (will always use the present one for systems w/ only 1 machine)
                    var deviceToUse = deviceId ?? runner.GetDeviceToUse(logger, apkRequiredArchitecture, "architecture");

                    if (deviceToUse == null)
                    {
                        return ExitCode.ADB_DEVICE_ENUMERATION_FAILURE;
                    }

                    runner.SetActiveDevice(deviceToUse);

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
            catch (Exception toLog)
            {
                logger.LogCritical(toLog, $"Failure to run test package: {toLog.Message}");
            }

            runner.UninstallApk(apkPackageName);
            return ExitCode.GENERAL_FAILURE;
        }
    }
}
