﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Logging;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared.Hardware;

public interface ISimulatorDevice : IDevice
{
    string SimRuntime { get; set; }
    string SimDeviceType { get; set; }
    public DeviceState State { get; }
    string DataPath { get; set; }
    string LogPath { get; set; }
    string SystemLog { get; }
    bool IsWatchSimulator { get; }
    Task Erase(ILog log);
    Task Shutdown(ILog log);
    Task<bool> PrepareSimulator(ILog log, params string[] bundleIdentifiers);
    Task KillEverything(ILog log);
    Task<bool> Boot(ILog log, CancellationToken cancellationToken);
}

public enum DeviceState
{
    Unknown = 0,
    Shutdown = 1,
    Booting = 2,
    Booted = 3,
    ShuttingDown = 4,
}
