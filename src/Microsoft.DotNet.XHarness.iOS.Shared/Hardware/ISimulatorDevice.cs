// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Logging;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared.Hardware
{
    public class SimRuntime
    {
        public string Name { get; }
        public string Identifier { get; }
        public long Version { get; }

        public SimRuntime(string name, string identifier, long version)
        {
            Name = name;
            Identifier = identifier;
            Version = version;
        }
    }

    public class SimDeviceType
    {
        public string Name { get; }
        public string Identifier { get; }
        public string ProductFamilyId { get; }
        public long MinRuntimeVersion { get; }
        public long MaxRuntimeVersion { get; }
        public bool Supports64Bits { get; }

        public SimDeviceType(string name, string identifier, string productFamilyId, long minRuntimeVersion, long maxRuntimeVersion, bool supports64Bits)
        {
            Name = name;
            Identifier = identifier;
            ProductFamilyId = productFamilyId;
            MinRuntimeVersion = minRuntimeVersion;
            MaxRuntimeVersion = maxRuntimeVersion;
            Supports64Bits = supports64Bits;
        }
    }

    public class SimDevicePair
    {
        public string UDID { get; }
        public string Companion { get; }
        public string Gizmo { get; }

        public SimDevicePair(string uDID, string companion, string gizmo)
        {
            UDID = uDID;
            Companion = companion;
            Gizmo = gizmo;
        }
    }

    public class SimDeviceSpecification
    {
        public SimulatorDevice Main { get; }
        public SimulatorDevice Companion { get; } // the phone for watch devices

        public SimDeviceSpecification(SimulatorDevice main, SimulatorDevice companion)
        {
            Main = main;
            Companion = companion;
        }
    }

    public interface ISimulatorDevice : IDevice
    {
        string SimRuntime { get; set; }
        string SimDeviceType { get; set; }
        string DataPath { get; set; }
        string LogPath { get; set; }
        string SystemLog { get; }
        bool IsWatchSimulator { get; }
        Task Erase(ILog log);
        Task Shutdown(ILog log);
        Task<bool> PrepareSimulator(ILog log, params string[] bundleIdentifiers);
        Task KillEverything(ILog log);
    }

    public interface ISimulatorLoader : IDeviceLoader
    {
        IEnumerable<SimRuntime> SupportedRuntimes { get; }
        IEnumerable<SimDeviceType> SupportedDeviceTypes { get; }
        IEnumerable<SimulatorDevice> AvailableDevices { get; }
        IEnumerable<SimDevicePair> AvailableDevicePairs { get; }
        Task<(ISimulatorDevice Simulator, ISimulatorDevice? CompanionSimulator)> FindSimulators(TestTarget target, ILog log, bool createIfNeeded = true, bool minVersion = false);
        Task<(ISimulatorDevice Simulator, ISimulatorDevice? CompanionSimulator)> FindSimulators(TestTargetOs target, ILog log, bool createIfNeeded = true, bool minVersion = false);
        ISimulatorDevice FindCompanionDevice(ILog log, ISimulatorDevice device);
        IEnumerable<ISimulatorDevice?> SelectDevices(TestTarget target, ILog log, bool min_version);
    }
}
