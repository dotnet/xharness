// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.DotNet.XHarness.Common;

namespace Microsoft.DotNet.XHarness.Android;

public static class AndroidEnvironmentReport
{
    public static ExecutionEnvironmentInfo CreateEnvironmentInfo(AdbRunner runner, AndroidDevice device)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(device);

        runner.PopulateEnvironmentInfo(device);

        return new ExecutionEnvironmentInfo
        {
            Host = EnvironmentReportLogger.GetHostEnvironmentInfo(),
            Target = new TargetEnvironmentInfo
            {
                Kind = device.IsEmulator ? "emulator" : "physical-device",
                Name = device.Model ?? device.DeviceSerial,
                Identifier = device.DeviceSerial,
                OperatingSystem = "Android",
                OperatingSystemVersion = device.OperatingSystemVersion,
                ApiLevel = device.ApiVersion,
                Architecture = device.Architecture,
                SupportedArchitectures = device.SupportedArchitectures?.ToArray(),
                Manufacturer = device.Manufacturer,
                Model = device.Model,
                ProductName = device.ProductName,
                BuildFingerprint = device.BuildFingerprint,
                CpuModel = device.CpuModel,
                CpuMaxFrequencyHertz = device.CpuMaxFrequencyKiloHertz * 1000,
                TotalMemoryBytes = device.TotalMemoryBytes,
            },
        };
    }
}
