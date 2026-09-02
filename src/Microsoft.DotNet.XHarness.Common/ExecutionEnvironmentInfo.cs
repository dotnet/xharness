// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.Common;

public sealed class ExecutionEnvironmentInfo
{
    public HostEnvironmentInfo? Host { get; set; }

    public TargetEnvironmentInfo? Target { get; set; }

    public TargetEnvironmentInfo? CompanionTarget { get; set; }
}

public sealed class HostEnvironmentInfo
{
    public string? MachineName { get; set; }

    public string? OperatingSystem { get; set; }

    public string? OperatingSystemArchitecture { get; set; }

    public string? ProcessArchitecture { get; set; }

    public string? FrameworkDescription { get; set; }

    public int LogicalProcessorCount { get; set; }

    public string? CpuModel { get; set; }

    public long? CpuMaxFrequencyHertz { get; set; }

    public long? TotalMemoryBytes { get; set; }
}

public sealed class TargetEnvironmentInfo
{
    public string? Kind { get; set; }

    public string? Name { get; set; }

    public string? Identifier { get; set; }

    public string? OperatingSystem { get; set; }

    public string? OperatingSystemVersion { get; set; }

    public int? ApiLevel { get; set; }

    public string? Architecture { get; set; }

    public string[]? SupportedArchitectures { get; set; }

    public string? Manufacturer { get; set; }

    public string? Model { get; set; }

    public string? ProductName { get; set; }

    public string? ProductType { get; set; }

    public string? HardwareModel { get; set; }

    public string? ModelNumber { get; set; }

    public string? Connection { get; set; }

    public string? BuildFingerprint { get; set; }

    public string? BuildVersion { get; set; }

    public string? SimulatorRuntime { get; set; }

    public string? SimulatorDeviceType { get; set; }

    public string? State { get; set; }

    public string? CpuModel { get; set; }

    public long? CpuMaxFrequencyHertz { get; set; }

    public long? TotalMemoryBytes { get; set; }

    public long? AvailableStorageBytes { get; set; }

    public long? TotalStorageBytes { get; set; }

    public string? DetailAvailability { get; set; }
}
