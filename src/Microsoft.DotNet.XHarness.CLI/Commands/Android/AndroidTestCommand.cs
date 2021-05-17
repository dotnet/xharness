// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
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
    internal class AndroidTestCommand : XHarnessCommand
    {
        private const string ReturnCodeVariableName = "return-code";

        private readonly AndroidTestCommandArguments _arguments = new();

        protected override XHarnessCommandArguments Arguments => _arguments;

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

        protected override Task<ExitCode> InvokeInternal(ILogger logger)
        {
            logger.LogDebug($"Android Test command called: App = {_arguments.AppPackagePath}{Environment.NewLine}Instrumentation Name = {_arguments.InstrumentationName}");
            logger.LogDebug($"Output Directory:{_arguments.OutputDirectory}{Environment.NewLine}Timeout = {_arguments.Timeout.TotalSeconds} seconds.");
            logger.LogDebug("Arguments to instrumentation:");

            if (!File.Exists(_arguments.AppPackagePath))
            {
                logger.LogCritical($"Couldn't find {_arguments.AppPackagePath}!");
                return Task.FromResult(ExitCode.PACKAGE_NOT_FOUND);
            }
            var runner = new AdbRunner(logger);

            // Assumption: APKs we test will only have one arch for now
            IEnumerable<string> apkRequiredArchitecture;

            if (_arguments.DeviceArchitecture.Any())
            {
                apkRequiredArchitecture = _arguments.DeviceArchitecture;
                logger.LogInformation($"Will attempt to run device on specified architecture: '{string.Join("', '", apkRequiredArchitecture)}'");
            }
            else
            {
                apkRequiredArchitecture = ApkHelper.GetApkSupportedArchitectures(_arguments.AppPackagePath);
                logger.LogInformation($"Will attempt to run device on detected architecture: '{string.Join("', '", apkRequiredArchitecture)}'");
            }

            // Package Name is not guaranteed to match file name, so it needs to be mandatory.
            string apkPackageName = _arguments.PackageName;
            string appPackagePath = _arguments.AppPackagePath;

            try
            {
                var exitCode = AndroidInstallCommand.InvokeHelper(
                    logger: logger,
                    apkPackageName: apkPackageName,
                    appPackagePath: appPackagePath,
                    apkRequiredArchitecture: apkRequiredArchitecture,
                    deviceId: null,
                    bootTimeoutSeconds: _arguments.LaunchTimeout,
                    runner: runner);

                if (exitCode == ExitCode.SUCCESS)
                {
                    exitCode = AndroidRunCommand.InvokeHelper(
                        logger: logger,
                        apkPackageName: apkPackageName,
                        instrumentationName: _arguments.InstrumentationName,
                        instrumentationArguments: _arguments.InstrumentationArguments,
                        outputDirectory: _arguments.OutputDirectory,
                        deviceOutputFolder: _arguments.DeviceOutputFolder,
                        timeout: _arguments.Timeout,
                        expectedExitCode: _arguments.ExpectedExitCode,
                        wifi: _arguments.Wifi,
                        runner: runner);
                } 

                runner.UninstallApk(apkPackageName);
                return Task.FromResult(exitCode);
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
    }
}
