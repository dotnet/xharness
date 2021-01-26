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

        protected override string CommandUsage { get; } = "android install [OPTIONS]";

        private const string CommandHelp = "Install .apk on an Android device without running it";
        protected override string CommandDescription { get; } = @$"
{CommandHelp}
 
Arguments:
";

        public AndroidInstallCommand() : base("install", false, CommandHelp)
        {
        }

        protected override Task<ExitCode> InvokeInternal(ILogger logger)
        {
            logger.LogDebug($"Android Install command called: App = {_arguments.AppPackagePath}{Environment.NewLine}");
            logger.LogDebug($"Timeout = {_arguments.Timeout.TotalSeconds} seconds.");

            if (!File.Exists(_arguments.AppPackagePath))
            {
                logger.LogCritical($"Couldn't find {_arguments.AppPackagePath}!");
                return Task.FromResult(ExitCode.PACKAGE_NOT_FOUND);
            }

            // Assumption: APKs we test will only have one arch for now
            string apkRequiredArchitecture;

            if (!string.IsNullOrEmpty(_arguments.DeviceArchitecture))
            {
                apkRequiredArchitecture = _arguments.DeviceArchitecture;
                logger.LogInformation($"Will attempt to run device on specified architecture: '{apkRequiredArchitecture}'");
            }
            else
            {
                apkRequiredArchitecture = ApkHelper.GetApkSupportedArchitectures(_arguments.AppPackagePath).First();
                logger.LogInformation($"Will attempt to run device on detected architecture: '{apkRequiredArchitecture}'");
            }

            // Package Name is not guaranteed to match file name, so it needs to be mandatory.
            string apkPackageName = _arguments.PackageName;

            string appPackagePath = _arguments.AppPackagePath;

            var runner = new AdbRunner(logger);

            return InvokeHelper(logger, apkPackageName, appPackagePath, apkRequiredArchitecture, runner);
        }

        public Task<ExitCode> InvokeHelper(ILogger logger, string apkPackageName, string appPackagePath, string apkRequiredArchitecture, AdbRunner runner)
        {
            try
            {
                using (logger.BeginScope("Initialization and setup of APK on device"))
                {
                    // Make sure the adb server is started
                    runner.StartAdbServer();

                    // enumerate the devices attached and their architectures
                    // Tell ADB to only use that one (will always use the present one for systems w/ only 1 machine)
                    var deviceToUse = runner.GetDeviceToUse(logger, apkRequiredArchitecture, "architecture");

                    if (deviceToUse == null)
                    {
                        return Task.FromResult(ExitCode.ADB_DEVICE_ENUMERATION_FAILURE);
                    }

                    runner.SetActiveDevice(deviceToUse);

                    // Wait til at least device(s) are ready
                    runner.WaitForDevice();

                    // Empty log as we'll be uploading the full logcat for this execution
                    runner.ClearAdbLog();

                    logger.LogDebug($"Working with {runner.GetAdbVersion()}");

                    // If anything changed about the app, Install will fail; uninstall it first.
                    // (we'll ignore if it's not present)
                    // This is where mismatched architecture APKs fail.
                    runner.UninstallApk(apkPackageName);
                    if (runner.InstallApk(appPackagePath) != 0)
                    {
                        logger.LogCritical("Install failure: Test command cannot continue");
                        return Task.FromResult(ExitCode.PACKAGE_INSTALLATION_FAILURE);
                    }
                    runner.KillApk(apkPackageName);
                }
                return Task.FromResult(ExitCode.SUCCESS);
            }
            catch (Exception toLog)
            {
                logger.LogCritical(toLog, $"Failure to run test package: {toLog.Message}");
            }

            return Task.FromResult(ExitCode.GENERAL_FAILURE);
        }
    }
}
