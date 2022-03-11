// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.XHarness.Android;
using Microsoft.DotNet.XHarness.CLI.AndroidHeadless;
using Microsoft.DotNet.XHarness.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.AndroidHeadless;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.AndroidHeadless;

internal class AndroidHeadlessDeviceCommand : AndroidHeadlessCommand<AndroidHeadlessDeviceCommandArguments>
{
    protected override AndroidHeadlessDeviceCommandArguments Arguments { get; } = new()
    {
        Verbosity = new VerbosityArgument(LogLevel.Error)
    };

    protected override string CommandUsage { get; } = "android-headless device [OPTIONS]";

    private const string CommandHelp = "Get ID of the device compatible with a given architecture";
    protected override string CommandDescription { get; } = @$"
{CommandHelp}
 
Arguments:
";

    public AndroidHeadlessDeviceCommand() : base("device", false, CommandHelp)
    {
    }

    protected override ExitCode InvokeCommand(ILogger logger)
    {
        IEnumerable<string>? executableRequiredArchitecture = null;

        if (Arguments.DeviceArchitecture.Value.Any())
        {
            executableRequiredArchitecture = Arguments.DeviceArchitecture.Value;
        }

        // Make sure the adb server is started
        var runner = new AdbRunner(logger);
        runner.StartAdbServer();

        // enumerate the devices attached and their architectures
        // Tell ADB to only use that one (will always use the present one for systems w/ only 1 machine)
        var device = runner.GetDevice(
            loadApiVersion: true,
            loadArchitecture: true,
            requiredApiVersion: Arguments.ApiVersion.Value,
            requiredArchitectures: executableRequiredArchitecture);

        if (device is null)
        {
            return ExitCode.DEVICE_NOT_FOUND;
        }

        DiagnosticsData.CaptureDeviceInfo(device);

        Console.WriteLine(device.DeviceSerial);

        return ExitCode.SUCCESS;
    }
}
