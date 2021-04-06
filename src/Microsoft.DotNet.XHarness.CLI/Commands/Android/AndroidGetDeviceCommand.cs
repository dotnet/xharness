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
    internal class AndroidGetDeviceCommand : XHarnessCommand
    {
        private readonly AndroidGetDeviceCommandArguments _arguments = new()
        {
            Verbosity = LogLevel.Error
        };

        protected override XHarnessCommandArguments Arguments => _arguments;

        protected override string CommandUsage { get; } = "android device --app=... [OPTIONS]";

        private const string CommandHelp = "Get Id of the device compatible with a given .apk";
        protected override string CommandDescription { get; } = @$"
{CommandHelp}
 
Arguments:
";

        public AndroidGetDeviceCommand() : base("device", false, CommandHelp)
        {
        }

        protected override Task<ExitCode> InvokeInternal(ILogger logger)
        {
            if (!File.Exists(_arguments.AppPackagePath))
            {
                logger.LogCritical($"Couldn't find {_arguments.AppPackagePath}!");
                return Task.FromResult(ExitCode.PACKAGE_NOT_FOUND);
            }

            var runner = new AdbRunner(logger);
            string apkRequiredArchitecture;

            if (!string.IsNullOrEmpty(_arguments.DeviceArchitecture))
            {
                apkRequiredArchitecture = _arguments.DeviceArchitecture;
            }
            else
            {
                apkRequiredArchitecture = ApkHelper.GetApkSupportedArchitectures(_arguments.AppPackagePath).First();
            }

            try
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

                Console.WriteLine(deviceToUse);

                return Task.FromResult(ExitCode.SUCCESS);
            }
            catch (Exception toLog)
            {
                logger.LogCritical(toLog, $"Failure to find compatible device: {toLog.Message}");
            }

            return Task.FromResult(ExitCode.GENERAL_FAILURE);
        }
    }
}
