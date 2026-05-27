// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;

namespace Microsoft.DotNet.XHarness.Apple;

internal static class AppleEnvironmentReport
{
    public static ExecutionEnvironmentInfo CreateMacCatalystEnvironment()
        => new()
        {
            Host = EnvironmentReportLogger.GetHostEnvironmentInfo(),
            Target = new TargetEnvironmentInfo
            {
                Kind = "maccatalyst",
                Name = Environment.MachineName,
                Identifier = Environment.MachineName,
                OperatingSystem = "macOS",
                OperatingSystemVersion = RuntimeInformation.OSDescription,
                Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            },
        };

    public static async Task<ExecutionEnvironmentInfo> CreateTargetEnvironmentAsync(
        IMlaunchProcessManager processManager,
        IDevice device,
        IDevice? companionDevice,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processManager);
        ArgumentNullException.ThrowIfNull(device);

        return new ExecutionEnvironmentInfo
        {
            Host = EnvironmentReportLogger.GetHostEnvironmentInfo(),
            Target = await CreateTargetInfoAsync(processManager, device, cancellationToken),
            CompanionTarget = companionDevice == null ? null : await CreateTargetInfoAsync(processManager, companionDevice, cancellationToken),
        };
    }

    private static async Task<TargetEnvironmentInfo> CreateTargetInfoAsync(
        IMlaunchProcessManager processManager,
        IDevice device,
        CancellationToken cancellationToken)
    {
        var targetInfo = new TargetEnvironmentInfo
        {
            Kind = device is IHardwareDevice ? "physical-device" : "simulator",
            Name = device.Name,
            Identifier = device.UDID,
            OperatingSystemVersion = device.OSVersion,
        };

        if (device is SimulatorDevice simulatorDevice)
        {
            targetInfo.OperatingSystem = device.OSVersion.Split(' ', 2)[0];
            targetInfo.SimulatorRuntime = simulatorDevice.SimRuntime;
            targetInfo.SimulatorDeviceType = simulatorDevice.SimDeviceType;
            targetInfo.State = simulatorDevice.State.ToString();
        }

        if (device is IHardwareDevice hardwareDevice)
        {
            targetInfo.OperatingSystem = hardwareDevice.DevicePlatform == DevicePlatform.Unknown
                ? null
                : hardwareDevice.DevicePlatform.AsString();
            targetInfo.Architecture = hardwareDevice.CpuArchitecture ?? hardwareDevice.Architecture.ToString();
            targetInfo.ProductType = hardwareDevice.ProductType;
            targetInfo.HardwareModel = hardwareDevice.HardwareModel;
            targetInfo.ModelNumber = hardwareDevice.ModelNumber;
            targetInfo.Connection = hardwareDevice.InterfaceType;
            targetInfo.BuildVersion = hardwareDevice.BuildVersion;
            targetInfo.AvailableStorageBytes = hardwareDevice.AmountDataAvailable;
            targetInfo.TotalStorageBytes = hardwareDevice.TotalDataCapacity;

            var detailedInfo = await TryGetDetailedDeviceInfoAsync(processManager, hardwareDevice.UDID, cancellationToken);
            targetInfo.CpuModel = detailedInfo.CpuModel;
            targetInfo.ProductName ??= detailedInfo.DeviceType;
            targetInfo.CpuMaxFrequencyHertz = detailedInfo.CpuMaxFrequencyHertz;
            targetInfo.TotalMemoryBytes = detailedInfo.TotalMemoryBytes;
            targetInfo.DetailAvailability = detailedInfo.DetailAvailability;
        }

        return targetInfo;
    }

    private static async Task<AppleDetailedDeviceInfo> TryGetDetailedDeviceInfoAsync(IMlaunchProcessManager processManager, string deviceIdentifier, CancellationToken cancellationToken)
    {
        if (processManager.XcodeVersion.Major < 15)
        {
            return new AppleDetailedDeviceInfo(DetailAvailability: "Unavailable (devicectl requires Xcode 15 or newer)");
        }

        var tempPath = Path.GetTempFileName();
        try
        {
            var commandLog = new Microsoft.DotNet.XHarness.Common.Logging.MemoryLog();
            var result = await processManager.ExecuteXcodeCommandAsync(
                "devicectl",
                new[] { "list", "devices", "-v", "--json-output", tempPath },
                commandLog,
                TimeSpan.FromMinutes(1),
                cancellationToken);

            if (!result.Succeeded || !File.Exists(tempPath))
            {
                return new AppleDetailedDeviceInfo(DetailAvailability: "Unavailable (devicectl device listing failed)");
            }

            var json = await File.ReadAllTextAsync(tempPath, cancellationToken);
            return ParseDetailedDeviceInfo(json, deviceIdentifier) ?? new AppleDetailedDeviceInfo(DetailAvailability: "Unavailable (matching devicectl entry not found)");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new AppleDetailedDeviceInfo(DetailAvailability: "Unavailable (failed to query devicectl metadata)");
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    private static AppleDetailedDeviceInfo? ParseDetailedDeviceInfo(string json, string deviceIdentifier)
    {
        using var document = JsonDocument.Parse(json);
        if (!TryFindDeviceElement(document.RootElement, deviceIdentifier, out var deviceElement))
        {
            return null;
        }

        return new AppleDetailedDeviceInfo(
            CpuModel: FindStringValueAtPath(deviceElement, "hardwareProperties", "cpuType", "name")
                ?? FindStringValue(deviceElement, "cpu", "chip", "processor", "soc", "socName"),
            DeviceType: FindStringValueAtPath(deviceElement, "hardwareProperties", "deviceType"),
            CpuMaxFrequencyHertz: FindInt64Value(deviceElement, "cpuMaxFrequency", "clockSpeed", "maxClockSpeed", "cpuFrequency", "processorSpeed"),
            TotalMemoryBytes: FindInt64Value(deviceElement, "totalMemory", "physicalMemory", "memory", "ram"),
            DetailAvailability: null);
    }

    private static bool TryFindDeviceElement(JsonElement element, string deviceIdentifier, out JsonElement matchingElement)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if ((property.Name.Equals("identifier", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Equals("udid", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Equals("deviceIdentifier", StringComparison.OrdinalIgnoreCase))
                    && property.Value.ValueKind == JsonValueKind.String
                    && string.Equals(property.Value.GetString(), deviceIdentifier, StringComparison.OrdinalIgnoreCase))
                {
                    matchingElement = element;
                    return true;
                }
            }

            foreach (var property in element.EnumerateObject())
            {
                if (TryFindDeviceElement(property.Value, deviceIdentifier, out matchingElement))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (TryFindDeviceElement(item, deviceIdentifier, out matchingElement))
                {
                    return true;
                }
            }
        }

        matchingElement = default;
        return false;
    }

    private static string? FindStringValue(JsonElement element, params string[] propertyNames)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                foreach (var propertyName in propertyNames)
                {
                    if (!property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (property.Value.ValueKind == JsonValueKind.String)
                    {
                        return property.Value.GetString();
                    }

                    if (property.Value.ValueKind == JsonValueKind.Number || property.Value.ValueKind == JsonValueKind.True || property.Value.ValueKind == JsonValueKind.False)
                    {
                        return property.Value.ToString();
                    }
                }

                var nestedValue = FindStringValue(property.Value, propertyNames);
                if (!string.IsNullOrWhiteSpace(nestedValue))
                {
                    return nestedValue;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nestedValue = FindStringValue(item, propertyNames);
                if (!string.IsNullOrWhiteSpace(nestedValue))
                {
                    return nestedValue;
                }
            }
        }

        return null;
    }

    private static string? FindStringValueAtPath(JsonElement element, params string[] propertyPath)
    {
        foreach (var pathPart in propertyPath)
        {
            if (element.ValueKind != JsonValueKind.Object || !TryGetProperty(element, pathPart, out element))
            {
                return null;
            }
        }

        return element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : element.ToString();
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static long? FindInt64Value(JsonElement element, params string[] propertyNames)
    {
        var rawValue = FindStringValue(element, propertyNames);
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        if (long.TryParse(rawValue, out var numericValue))
        {
            return numericValue;
        }

        if (double.TryParse(rawValue, out var floatingValue))
        {
            return (long)floatingValue;
        }

        return null;
    }

    private sealed record AppleDetailedDeviceInfo(string? CpuModel = null, string? DeviceType = null, long? CpuMaxFrequencyHertz = null, long? TotalMemoryBytes = null, string? DetailAvailability = null);
}
