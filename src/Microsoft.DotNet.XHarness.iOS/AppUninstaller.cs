// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution.Mlaunch;

namespace Microsoft.DotNet.XHarness.iOS
{
    public class AppUninstaller
    {
        private readonly IMLaunchProcessManager _processManager;
        private readonly ILog _mainLog;
        private readonly int _verbosity;

        public AppUninstaller(IMLaunchProcessManager processManager, ILog mainLog, int verbosity)
        {
            _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
            _mainLog = mainLog ?? throw new ArgumentNullException(nameof(mainLog));
            _verbosity = verbosity;
        }

        public async Task<ProcessExecutionResult> UninstallApp(string deviceName, string appBundleId, CancellationToken cancellationToken = default)
        {
            var args = new MlaunchArguments();

            for (int i = -1; i < _verbosity; i++)
            {
                args.Add(new VerbosityArgument());
            }

            args.Add(new UninstallAppFromDeviceArgument(appBundleId));
            args.Add(new DeviceNameArgument(deviceName));

            return await _processManager.ExecuteCommandAsync(args, _mainLog, TimeSpan.FromMinutes(1), cancellationToken: cancellationToken);
        }
    }
}
