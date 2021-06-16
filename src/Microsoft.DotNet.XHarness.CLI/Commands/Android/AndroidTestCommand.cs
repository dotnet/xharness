// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Android;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.CLI.Commands;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Android
{
    internal class AndroidTestCommand : XHarnessCommand<AndroidTestCommandArguments>
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

        protected override async Task<ExitCode> InvokeInternal(ILogger logger)
        {
            logger.LogDebug($"Android Test command called: App = {Arguments.AppPackagePath}{Environment.NewLine}Instrumentation Name = {Arguments.InstrumentationName}");
            logger.LogDebug($"Output Directory:{Arguments.OutputDirectory}{Environment.NewLine}Timeout = {Arguments.Timeout.Value.TotalSeconds} seconds.");
            logger.LogDebug("Arguments to instrumentation:");

            if (!File.Exists(Arguments.AppPackagePath))
            {
                logger.LogCritical($"Couldn't find {Arguments.AppPackagePath}!");
                return ExitCode.PACKAGE_NOT_FOUND;
            }
            var runner = new AdbRunner(logger);

            IEnumerable<string> apkRequiredArchitecture;

            if (Arguments.DeviceArchitecture.Value.Any())
            {
                apkRequiredArchitecture = Arguments.DeviceArchitecture.Value;
                logger.LogInformation($"Will attempt to run device on specified architecture: '{string.Join("', '", apkRequiredArchitecture)}'");
            }
            else
            {
                apkRequiredArchitecture = ApkHelper.GetApkSupportedArchitectures(Arguments.AppPackagePath);
                logger.LogInformation($"Will attempt to run device on detected architecture: '{string.Join("', '", apkRequiredArchitecture)}'");
            }

            // Package Name is not guaranteed to match file name, so it needs to be mandatory.
            string apkPackageName = Arguments.PackageName;
            string appPackagePath = Arguments.AppPackagePath;

            var webServerCts = new CancellationTokenSource();
            try
            {
                var exitCode = AndroidInstallCommand.InvokeHelper(
                    logger: logger,
                    apkPackageName: apkPackageName,
                    appPackagePath: appPackagePath,
                    apkRequiredArchitecture: apkRequiredArchitecture,
                    deviceId: null,
                    bootTimeoutSeconds: Arguments.LaunchTimeout,
                    runner: runner);

                if (exitCode == ExitCode.SUCCESS)
                {
                    ServerURLs? serverURLs = null;
                    Dictionary<string, string> instrumentationArgs = _arguments.InstrumentationArguments;
                    if (_arguments.WebServerMiddlewarePathsAndTypes.Count > 0)
                    {
                        serverURLs = await WebServer.Start(
                            _arguments, logger,
                            null,
                            webServerCts.Token);
                        webServerCts.CancelAfter(_arguments.Timeout);

                        foreach (var envVariable in _arguments.SetWebServerEnvironmentVariablesHttp)
                        {
                            instrumentationArgs.Add($"env:{envVariable}", serverURLs!.Http);
                        }

                        foreach (var envVariable in _arguments.SetWebServerEnvironmentVariablesHttps)
                        {
                            instrumentationArgs.Add($"env:{envVariable}", serverURLs!.Https);
                        }
                    }
                    exitCode = AndroidRunCommand.InvokeHelper(
                        logger: logger,
                        apkPackageName: apkPackageName,
                        instrumentationName: Arguments.InstrumentationName,
                        instrumentationArguments: instrumentationArgs,
                        outputDirectory: Arguments.OutputDirectory,
                        deviceOutputFolder: Arguments.DeviceOutputFolder,
                        timeout: Arguments.Timeout,
                        expectedExitCode: Arguments.ExpectedExitCode,
                        wifi: Arguments.Wifi,
                        runner: runner);
                } 

                runner.UninstallApk(apkPackageName);
                return exitCode;
            }
            catch (NoDeviceFoundException noDevice)
            {
                logger.LogCritical(noDevice, noDevice.Message);
                return ExitCode.ADB_DEVICE_ENUMERATION_FAILURE;
            }
            catch (Exception toLog)
            {
                logger.LogCritical(toLog, toLog.Message);
            }
            finally
            {
                if (!webServerCts.IsCancellationRequested)
                {
                    webServerCts.Cancel();
                }
            }

            return ExitCode.GENERAL_FAILURE;
        }
    }
}
