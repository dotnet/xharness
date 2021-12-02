// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Android;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Android;

internal class AndroidGetStateCommand : GetStateCommand<AndroidGetStateCommandArguments>
{
    protected override string CommandUsage { get; } = "android state";

    protected override AndroidGetStateCommandArguments Arguments { get; } = new();

    public AndroidGetStateCommand() : base(TargetPlatform.Android, new ServiceCollection())
    {
    }

    protected override Task<ExitCode> InvokeInternal(ILogger logger)
    {
        var runner = new AdbRunner(logger);

        logger.LogInformation("Getting state of ADB and attached Android device(s)");
        try
        {
            string state = runner.GetAdbState();
            if (string.IsNullOrEmpty(state))
            {
                state = "No device attached";
            }
            logger.LogInformation($"ADB Version info:{Environment.NewLine}{runner.GetAdbVersion()}");
            logger.LogInformation($"ADB State ('device' if physically attached):{Environment.NewLine}{state}");

            logger.LogInformation($"List of devices:");
            var deviceAndArchList = runner.GetAttachedDevicesWithProperties("architecture");
            foreach (string device in deviceAndArchList.Keys)
            {
                logger.LogInformation($"Device: '{device}' - Architecture: {deviceAndArchList[device]}");
            }

            return Task.FromResult(ExitCode.SUCCESS);
        }
        catch (Exception toLog)
        {
            logger.LogCritical(toLog, $"Error: {toLog.Message}");
            return Task.FromResult(ExitCode.GENERAL_FAILURE);
        }
    }
}
