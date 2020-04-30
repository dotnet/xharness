// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution.Mlaunch;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;

namespace Microsoft.DotNet.XHarness.iOS
{
    public class AppInstaller
    {
        private readonly IProcessManager _processManager;
        private readonly IHardwareDeviceLoader _deviceLoader;
        private readonly ILog _mainLog;
        private readonly int _verbosity;

        public AppInstaller(IProcessManager processManager, IHardwareDeviceLoader deviceLoader, ILog mainLog, int verbosity)
        {
            _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
            _deviceLoader = deviceLoader ?? throw new ArgumentNullException(nameof(deviceLoader));
            _mainLog = mainLog ?? throw new ArgumentNullException(nameof(mainLog));
            _verbosity = verbosity;
        }

        public async Task<(string deviceName, ProcessExecutionResult result)> InstallApp(string appPath, TestTarget target, string? deviceName = null, CancellationToken cancellationToken = default)
        {
            if (target.IsSimulator())
            {
                // We reset the simulator when running, so a separate install step does not make much sense.
                throw new InvalidOperationException("Installing to a simulator is not supported.");
            }

            if (!Directory.Exists(appPath))
            {
                throw new DirectoryNotFoundException("Failed to find the app bundle directory");
            }

            if (deviceName == null)
            {
                var device = await _deviceLoader.FindDevice(target.ToRunMode(), _mainLog, false, false);

                if (target.IsWatchOSTarget())
                {
                    deviceName = (await _deviceLoader.FindCompanionDevice(_mainLog, device)).Name;
                }
                else
                {
                    deviceName = device.Name;
                }
            }

            if (deviceName == null)
            {
                throw new NoDeviceFoundException();
            }

            var args = new MlaunchArguments();

            for (int i = -1; i < _verbosity; i++)
            {
                args.Add(new VerbosityArgument());
            }

            args.Add(new InstallAppOnDeviceArgument(appPath));
            args.Add(new DeviceNameArgument(deviceName));

            if (target.IsWatchOSTarget())
            {
                args.Add(new DeviceArgument("ios,watchos"));
            }

            var totalSize = Directory.GetFiles(appPath, "*", SearchOption.AllDirectories).Select((v) => new FileInfo(v).Length).Sum();
            _mainLog.WriteLine($"Installing '{appPath}' to '{deviceName}' ({totalSize / 1024.0 / 1024.0:N2} MB)");

            ProcessExecutionResult result = await _processManager.ExecuteCommandAsync(args, _mainLog, TimeSpan.FromHours(1), cancellationToken: cancellationToken);

            return (deviceName, result);
        }
    }
}
