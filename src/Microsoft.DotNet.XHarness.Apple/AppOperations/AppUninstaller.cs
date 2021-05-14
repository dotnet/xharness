// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;

namespace Microsoft.DotNet.XHarness.Apple
{
    public class AppUninstaller
    {
        private readonly IMlaunchProcessManager _processManager;
        private readonly ILog _mainLog;

        public AppUninstaller(IMlaunchProcessManager processManager, ILog mainLog)
        {
            _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
            _mainLog = mainLog ?? throw new ArgumentNullException(nameof(mainLog));
        }

        public Task<ProcessExecutionResult> UninstallApp(IDevice simulator, string appBundleId, CancellationToken cancellationToken = default)
            => _processManager.ExecuteXcodeCommandAsync(
                "simctl",
                new[] { "uninstall", simulator.UDID, appBundleId },
                _mainLog,
                TimeSpan.FromMinutes(3),
                cancellationToken: cancellationToken);

        public Task<ProcessExecutionResult> UninstallDeviceApp(IDevice device, string appBundleId, CancellationToken cancellationToken = default)
        {
            var args = new MlaunchArguments
            {
                new UninstallAppFromDeviceArgument(appBundleId),
                new DeviceNameArgument(device)
            };

            return _processManager.ExecuteCommandAsync(args, _mainLog, TimeSpan.FromMinutes(3), cancellationToken: cancellationToken);
        }
    }
}
