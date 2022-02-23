// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Android;
using Microsoft.DotNet.XHarness.CLI.Android;
using Microsoft.DotNet.XHarness.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Android;

internal class AndroidGetDeviceCommand : AndroidCommand<AndroidGetDeviceCommandArguments>
{
    protected override AndroidGetDeviceCommandArguments Arguments { get; } = new()
    {
        Verbosity = new VerbosityArgument(LogLevel.Error)
    };

    protected override string CommandUsage { get; } = "android device [OPTIONS]";

    private const string CommandHelp = "Get ID of the device compatible with a given .apk / architecture";
    protected override string CommandDescription { get; } = @$"
{CommandHelp}
 
Arguments:
";

    public AndroidGetDeviceCommand() : base("device", false, CommandHelp)
    {
    }

    protected override Task<ExitCode> InvokeInternal(ILogger logger)
    {
        var runner = new AdbRunner(logger);
        IEnumerable<string>? apkRequiredArchitecture = null;

        if (Arguments.DeviceArchitecture.Value.Any())
        {
            apkRequiredArchitecture = Arguments.DeviceArchitecture.Value;
        }
        else if (!string.IsNullOrEmpty(Arguments.AppPackagePath.Value))
        {
            if (!File.Exists(Arguments.AppPackagePath.Value))
            {
                logger.LogCritical($"Couldn't find {Arguments.AppPackagePath.Value}!");
                return Task.FromResult(ExitCode.PACKAGE_NOT_FOUND);
            }

            apkRequiredArchitecture = ApkHelper.GetApkSupportedArchitectures(Arguments.AppPackagePath.Value);
        }

        try
        {
            // Make sure the adb server is started
            runner.StartAdbServer();

            // enumerate the devices attached and their architectures
            // Tell ADB to only use that one (will always use the present one for systems w/ only 1 machine)
            var device = runner.GetDevice(
                loadApiVersion: true,
                loadArchitecture: true,
                requiredApiVersion: Arguments.ApiVersion.Value,
                requiredArchitectures: apkRequiredArchitecture);

            if (device is null)
            {
                return Task.FromResult(ExitCode.ADB_DEVICE_ENUMERATION_FAILURE);
            }

            DiagnosticsData.CaptureDeviceInfo(device);

            Console.WriteLine(device.DeviceSerial);

            return Task.FromResult(ExitCode.SUCCESS);
        }
        catch (NoDeviceFoundException noDevice)
        {
            logger.LogCritical(noDevice, $"Failure to find compatible device: {noDevice.Message}");
            return Task.FromResult(ExitCode.ADB_DEVICE_ENUMERATION_FAILURE);
        }
        catch (Exception toLog)
        {
            logger.LogCritical(toLog, $"Failure to find compatible device: {toLog.Message}");
        }

        return Task.FromResult(ExitCode.GENERAL_FAILURE);
    }
}
