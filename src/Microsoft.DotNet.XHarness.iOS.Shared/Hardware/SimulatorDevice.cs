// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Hardware
{

    public class SimulatorDevice : ISimulatorDevice
    {
        private readonly IMlaunchProcessManager _processManager;
        private readonly ITCCDatabase _tCCDatabase;

        public string UDID { get; set; }
        public string Name { get; set; }
        public string SimRuntime { get; set; }
        public string SimDeviceType { get; set; }
        public string DataPath { get; set; }
        public string LogPath { get; set; }
        public string SystemLog => Path.Combine(LogPath, "system.log");


        public SimulatorDevice(IMlaunchProcessManager processManager, ITCCDatabase tccDatabase)
        {
            _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
            _tCCDatabase = tccDatabase ?? throw new ArgumentNullException(nameof(tccDatabase));
        }

        public bool IsWatchSimulator => SimRuntime.StartsWith("com.apple.CoreSimulator.SimRuntime.watchOS", StringComparison.Ordinal);

        public string OSVersion
        {
            get
            {
                var v = SimRuntime.Substring("com.apple.CoreSimulator.SimRuntime.".Length);
                var dash = v.IndexOf('-');
                return v.Substring(0, dash) + " " + v.Substring(dash + 1).Replace('-', '.');
            }
        }

        public async Task Erase(ILog log)
        {
            // here we don't care if execution fails.
            // erase the simulator (make sure the device isn't running first)
            await _processManager.ExecuteXcodeCommandAsync("simctl", new[] { "shutdown", UDID }, log, TimeSpan.FromMinutes(1));
            await _processManager.ExecuteXcodeCommandAsync("simctl", new[] { "erase", UDID }, log, TimeSpan.FromMinutes(1));

            // boot & shutdown to make sure it actually works
            await _processManager.ExecuteXcodeCommandAsync("simctl", new[] { "boot", UDID }, log, TimeSpan.FromMinutes(1));
            await _processManager.ExecuteXcodeCommandAsync("simctl", new[] { "shutdown", UDID }, log, TimeSpan.FromMinutes(1));
        }

        public async Task Shutdown(ILog log) => await _processManager.ExecuteXcodeCommandAsync("simctl", new[] { "shutdown", UDID }, log, TimeSpan.FromMinutes(1));

        public async Task KillEverything(ILog log)
        {
            await _processManager.ExecuteCommandAsync("launchctl", new[] { "remove", "com.apple.CoreSimulator.CoreSimulatorService" }, log, TimeSpan.FromSeconds(10));

            var toKill = new string[] { "iPhone Simulator", "iOS Simulator", "Simulator", "Simulator (Watch)", "com.apple.CoreSimulator.CoreSimulatorService", "ibtoold" };

            var args = new List<string>
            {
                "-9"
            };
            args.AddRange(toKill);

            await _processManager.ExecuteCommandAsync("killall", args, log, TimeSpan.FromSeconds(10));

            var dirsToBeDeleted = new[] {
                Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), "Library", "Saved Application State", "com.apple.watchsimulator.savedState"),
                Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), "Library", "Saved Application State", "com.apple.iphonesimulator.savedState"),
            };

            foreach (var dir in dirsToBeDeleted)
            {
                try
                {
                    if (Directory.Exists(dir))
                    {
                        Directory.Delete(dir, true);
                    }
                }
                catch (Exception e)
                {
                    log.WriteLine("Could not delete the directory '{0}': {1}", dir, e.Message);
                }
            }
        }

        private async Task OpenSimulator(ILog log)
        {
            string simulator_app;

            if (IsWatchSimulator && _processManager.XcodeVersion.Major < 9)
            {
                simulator_app = Path.Combine(_processManager.XcodeRoot, "Contents", "Developer", "Applications", "Simulator (Watch).app");
            }
            else
            {
                simulator_app = Path.Combine(_processManager.XcodeRoot, "Contents", "Developer", "Applications", "Simulator.app");
                if (!Directory.Exists(simulator_app))
                {
                    simulator_app = Path.Combine(_processManager.XcodeRoot, "Contents", "Developer", "Applications", "iOS Simulator.app");
                }
            }

            await _processManager.ExecuteCommandAsync("open", new[] { "-a", simulator_app, "--args", "-CurrentDeviceUDID", UDID }, log, TimeSpan.FromSeconds(15));
        }

        public async Task<bool> PrepareSimulator(ILog log, params string[] bundleIdentifiers)
        {
            // Kill all existing processes
            await KillEverything(log);

            // We shutdown and erase all simulators.
            await Erase(log);

            // Edit the permissions to prevent dialog boxes in the test app
            var tccDB = Path.Combine(DataPath, "data", "Library", "TCC", "TCC.db");
            if (!File.Exists(tccDB))
            {
                log.WriteLine("Opening simulator to create TCC.db");
                await OpenSimulator(log);

                var tccCreationTimeout = 60;
                var watch = new Stopwatch();
                watch.Start();
                while (!File.Exists(tccDB) && watch.Elapsed.TotalSeconds < tccCreationTimeout)
                {
                    log.WriteLine("Waiting for simulator to create TCC.db... {0}", (int)(tccCreationTimeout - watch.Elapsed.TotalSeconds));
                    await Task.Delay(TimeSpan.FromSeconds(0.250));
                }
            }

            var result = true;
            if (File.Exists(tccDB))
            {
                result &= await _tCCDatabase.AgreeToPromptsAsync(SimRuntime, tccDB, UDID, log, bundleIdentifiers);
            }
            else
            {
                log.WriteLine("No TCC.db found for the simulator {0} (SimRuntime={1} and SimDeviceType={1})", UDID, SimRuntime, SimDeviceType);
            }

            // Make sure we're in a clean state
            await KillEverything(log);

            // Make 100% sure we're shutdown
            await Shutdown(log);

            return result;
        }

    }
}
