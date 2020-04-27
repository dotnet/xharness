// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;

using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.Commands.iOS
{
    internal class iOSGetStateCommand : GetStateCommand
    {

        class DeviceInfo
        {
            public string Name { get; set; }
            public string UDID { get; set; }
            public string Type { get; set; }
            public string OSVersion { get; set; }
        }

        class SystemInfo
        {
            public string MachineName { get; set; }
            public string OSName { get; set; }
            public string OSVersion { get; set; }
            public string OSPlatform { get; set; }
            public string XcodePath { get; set; }
            public string XcodeVersion { get; set; }
            public List<DeviceInfo> Simulators { get; } = new List<DeviceInfo>();
            public List<DeviceInfo> Devices { get; } = new List<DeviceInfo>();
        }

        private const string SimulatorPrefix = "com.apple.CoreSimulator.SimDeviceType.";
        private bool _showSimulatorsUUID = false;
        private bool _showDevicesUUID = true;
        private bool _useJson = false;

        public iOSGetStateCommand() : base()
        {
            Options = new OptionSet() {
                "usage: ios state",
                "",
                "Print information about the current machine, such as host machine info and device status",
                { "include-simulator-uuid:", "Include the simulators UUID. Defaults to false.", v =>  bool.TryParse(v, out _showSimulatorsUUID)},
                { "include-devices-uuid:", "Include the devices UUID.", v => bool.TryParse(v, out _showDevicesUUID)},
                { "json", "Use json as the output format.", v => _useJson = v != null},
            };
        }

        private async Task AsJson(SystemInfo info)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            await JsonSerializer.SerializeAsync(Console.OpenStandardOutput(), info, options);
            Console.WriteLine();
        }

        private void AsText(SystemInfo info)
        {
            Console.WriteLine("Runtime Enviroment:");
            Console.WriteLine($"  Machine name:\t{info.MachineName}");
            Console.WriteLine($"  OS Name:\t{info.OSName}");
            Console.WriteLine($"  OS Version:\t{info.OSVersion}");
            Console.WriteLine($"  OS Platform:\t{info.OSPlatform}");

            Console.WriteLine();

            Console.WriteLine("Developer Tools:");

            Console.WriteLine($"  Xcode:\t{info.XcodePath} - ({info.XcodeVersion})");

            Console.WriteLine();

            Console.WriteLine("Installed Simulators:");

            foreach (var sim in info.Simulators)
            {
                var uuid = _showSimulatorsUUID ? $" ({sim.UDID})" : "";
                Console.WriteLine($"  {sim.Name}{uuid}: {sim.Type} {sim.OSVersion})");
            }

            Console.WriteLine();

            Console.WriteLine("Connected Devices:");

            foreach (var dev in info.Devices)
            {
                var uuid = _showDevicesUUID ? $" ({dev.UDID})" : "";
                Console.WriteLine($"  {dev.Name}{uuid}: {dev.Type} {dev.OSVersion})");
            }

        }

        protected override async Task<ExitCode> InvokeInternal()
        {
            var processManager = new ProcessManager(null); 
            var deviceLoader = new HardwareDeviceLoader(processManager);
            var simulatorLoader = new SimulatorLoader(processManager);
            var log = new MemoryLog(); // do we really want to log this?

            // build the required data, then depending on the format print out
            var info = new SystemInfo
            {
                MachineName = Environment.MachineName,
                OSName = "Mac OS X",
                OSVersion = Darwin.GetVersion() ?? "",
                OSPlatform = "Darwin",
                XcodePath = processManager.XcodeRoot,
                XcodeVersion = processManager.XcodeVersion.ToString()
            };

            await simulatorLoader.LoadDevices(log);

            foreach (var sim in simulatorLoader.AvailableDevices)
            {
                info.Simulators.Add(new DeviceInfo
                {
                    Name = sim.Name,
                    UDID = sim.UDID,
                    Type = sim.SimDeviceType.Remove(0, SimulatorPrefix.Length).Replace('-', ' '),
                    OSVersion = sim.OSVersion,
                });
            }

            await deviceLoader.LoadDevices(log);

            foreach (var dev in deviceLoader.ConnectedDevices)
            {
                info.Devices.Add(new DeviceInfo
                {
                    Name = dev.Name,
                    UDID = dev.DeviceIdentifier,
                    Type = $"{dev.DeviceClass} {dev.DevicePlatform}",
                    OSVersion = dev.OSVersion,
                });
            }

            if (_useJson)
            {
                await AsJson(info);
            }
            else
            {
                AsText(info);
            }
            return ExitCode.SUCCESS;
        }
    }
}
