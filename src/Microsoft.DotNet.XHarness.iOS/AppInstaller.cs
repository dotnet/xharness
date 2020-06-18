// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution.Mlaunch;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;

namespace Microsoft.DotNet.XHarness.iOS
{
    public class AppInstaller
    {
        private readonly IMLaunchProcessManager _processManager;
        private readonly IHardwareDeviceLoader _deviceLoader;
        private readonly ILog _mainLog;
        private readonly int _verbosity;

        public AppInstaller(IMLaunchProcessManager processManager, IHardwareDeviceLoader deviceLoader, ILog mainLog, int verbosity)
        {
            _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
            _deviceLoader = deviceLoader ?? throw new ArgumentNullException(nameof(deviceLoader));
            _mainLog = mainLog ?? throw new ArgumentNullException(nameof(mainLog));
            _verbosity = verbosity;
        }

        public async Task<(string deviceName, ProcessExecutionResult result)> InstallApp(AppBundleInformation appBundleInformation, TestTargetOs target, string? deviceName = null, CancellationToken cancellationToken = default)
        {
            if (target.Platform.IsSimulator())
            {
                // We reset the simulator when running, so a separate install step does not make much sense.
                throw new InvalidOperationException("Installing to a simulator is not supported.");
            }

            if (!Directory.Exists(appBundleInformation.LaunchAppPath))
            {
                throw new DirectoryNotFoundException("Failed to find the app bundle directory");
            }

            if (deviceName == null)
            {
                // the _deviceLoader.FindDevice will return the fist device of the type, but we want to make sure that
                // the device we use is if the correct arch, therefore, we will use the LoadDevices and return the
                // correct one
                await _deviceLoader.LoadDevices(_mainLog, false, false);
                IHardwareDevice? device = null;
                if (appBundleInformation.Supports32Bit)
                {
                    // we only support 32b on iOS, therefore we can ignore the target
                    device = _deviceLoader.Connected32BitIOS.FirstOrDefault();
                }
                else
                {
                    device = target.Platform switch
                    {
                        TestTarget.Device_iOS => _deviceLoader.Connected64BitIOS.FirstOrDefault(),
                        TestTarget.Device_tvOS => _deviceLoader.ConnectedTV.FirstOrDefault(),
                        _ => device
                    };
                }

                deviceName = target.Platform.IsWatchOSTarget() ? (await _deviceLoader.FindCompanionDevice(_mainLog, device)).Name : device?.Name;
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

            args.Add(new InstallAppOnDeviceArgument(appBundleInformation.LaunchAppPath));
            args.Add(new DeviceNameArgument(deviceName));

            if (target.Platform.IsWatchOSTarget())
            {
                args.Add(new DeviceArgument("ios,watchos"));
            }

            var totalSize = Directory.GetFiles(appBundleInformation.LaunchAppPath, "*", SearchOption.AllDirectories).Select((v) => new FileInfo(v).Length).Sum();
            _mainLog.WriteLine($"Installing '{appBundleInformation.LaunchAppPath}' to '{deviceName}' ({totalSize / 1024.0 / 1024.0:N2} MB)");

            ProcessExecutionResult result = await _processManager.ExecuteCommandAsync(args, _mainLog, TimeSpan.FromHours(1), cancellationToken: cancellationToken);

            return (deviceName, result);
        }
    }
}
