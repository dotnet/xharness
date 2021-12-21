// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
        try
        {
            var data = GetStateData(logger);

            if (Arguments.UseJson)
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                JsonSerializer.Serialize(Console.OpenStandardOutput(), data, options);
                return Task.FromResult(ExitCode.SUCCESS);
            }

            var state = data.DeviceState switch
            {
                "device" => "Device/emulator is ready",
                null or "" => "No device attached",
                _ => data.DeviceState,
            };

            void PrintDeviceInfo(DeviceInfo device)
            {
                logger.LogInformation($"{device.Name}:{Environment.NewLine}" +
                    $"  Architecture: {device.Architecture}{Environment.NewLine}" +
                    $"  Supported architectures: {string.Join(", ", device.SupportedArchitectures)}");
            }

            logger.LogInformation($"ADB Version info:{Environment.NewLine}{string.Join(Environment.NewLine, data.AdbVersion)}");
            logger.LogInformation($"ADB State:{Environment.NewLine}{state}");

            if (data.Emulators.Any())
            {
                logger.LogInformation($"List of emulators:");
                foreach (DeviceInfo emulator in data.Emulators)
                {
                    PrintDeviceInfo(emulator);
                }
            }

            if (data.Devices.Any())
            {
                logger.LogInformation($"List of devices:");
                foreach (DeviceInfo device in data.Devices)
                {
                    PrintDeviceInfo(device);
                }
            }

            return Task.FromResult(ExitCode.SUCCESS);
        }
        catch (Exception toLog)
        {
            logger.LogCritical(toLog, $"Error: {toLog.Message}");
            return Task.FromResult(ExitCode.GENERAL_FAILURE);
        }
    }

    private static StateData GetStateData(ILogger logger)
    {
        var runner = new AdbRunner(logger);

        logger.LogDebug("Getting state of ADB and attached Android device(s)");

        var adbVersion = runner.GetAdbVersion()
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var state = runner.GetAdbState().Trim();

        Dictionary<string, string?> deviceAndArchList = runner.GetAttachedDevicesWithProperties("architecture");
        List<DeviceInfo> allDevices = deviceAndArchList
            .Select(d => new DeviceInfo(
                Name: d.Key,
                Architecture: runner.GetDeviceArchitecture(logger, d.Key) ?? "unknown",
                SupportedArchitectures: d.Value?.Split(',') ?? Enumerable.Empty<string>()))
            .ToList();

        var emulators = allDevices.Where(d => d.Name.StartsWith("emulator"));
        var devices = allDevices.Except(emulators);

        return new StateData(state, adbVersion, emulators, devices);
    }

    private record StateData(string DeviceState, string[] AdbVersion, IEnumerable<DeviceInfo> Emulators, IEnumerable<DeviceInfo> Devices);

    private record DeviceInfo(string Name, string Architecture, IEnumerable<string> SupportedArchitectures);
}
