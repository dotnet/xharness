// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
    internal class AndroidTestCommand : XHarnessCommand
    {
        private const string ReturnCodeVariableName = "return-code";

        private readonly AndroidTestCommandArguments _arguments = new AndroidTestCommandArguments();

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

            var installer = new AndroidInstallCommand();
            var testRunner = new AndroidRunCommand();

            try
            {
                var exitCode = installer.InvokeHelper(
                    logger: logger,
                    apkPackageName: apkPackageName,
                    appPackagePath: appPackagePath,
                    apkRequiredArchitecture: apkRequiredArchitecture,
                    deviceId: null,
                    runner: runner);

                if (exitCode == ExitCode.SUCCESS)
                {
                    exitCode = testRunner.InvokeHelper(
                    logger: logger,
                    apkPackageName: apkPackageName,
                    instrumentationName: _arguments.InstrumentationName,
                    instrumentationArguments: _arguments.InstrumentationArguments,
                    outputDirectory: _arguments.OutputDirectory,
                    deviceOutputFolder: _arguments.DeviceOutputFolder,
                    timeout: _arguments.Timeout,
                    expectedExitCode: _arguments.ExpectedExitCode,
                    runner: runner);
                } 

                runner.UninstallApk(apkPackageName);
                return Task.FromResult(exitCode);
            }
            catch (Exception toLog)
            {
                logger.LogCritical(toLog, toLog.Message);
            }

            return Task.FromResult(ExitCode.GENERAL_FAILURE);
        }
    }
}
