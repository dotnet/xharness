// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;

namespace Microsoft.DotNet.XHarness.Apple
{
    public abstract class AppRunnerBase
    {
        private readonly IFileBackedLog _mainLog;
        private readonly IHardwareDeviceLoader _hardwareDeviceLoader;
        private readonly IProcessManager _processManager;

        protected AppRunnerBase(
            IProcessManager processManager,
            IHardwareDeviceLoader hardwareDeviceLoader,
            IFileBackedLog mainLog,
            Action<string>? logCallback = null)
        {
            _hardwareDeviceLoader = hardwareDeviceLoader ?? throw new ArgumentNullException(nameof(hardwareDeviceLoader));
            _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));

            if (logCallback == null)
            {
                _mainLog = mainLog ?? throw new ArgumentNullException(nameof(mainLog));
            }
            else
            {
                // create using the main as the default log
                _mainLog = Log.CreateReadableAggregatedLog(mainLog, new CallbackLog(logCallback));
            }
        }

        protected async Task<string> FindDevice(TestTargetOs target)
        {
            IHardwareDevice? companionDevice = null;
            IHardwareDevice device = await _hardwareDeviceLoader.FindDevice(target.Platform.ToRunMode(), _mainLog, includeLocked: false, force: false);

            if (target.Platform.IsWatchOSTarget())
            {
                companionDevice = await _hardwareDeviceLoader.FindCompanionDevice(_mainLog, device);
            }

            return companionDevice?.Name ?? device.Name;
        }

        protected Task<ProcessExecutionResult> RunMacCatalystApp(
            AppBundleInformation appInfo,
            TimeSpan timeout,
            IEnumerable<string> appArguments,
            Dictionary<string, object> environmentVariables,
            CancellationToken cancellationToken)
        {
            var binaryPath = Path.Combine(appInfo.AppPath, "Contents", "MacOS", appInfo.BundleExecutable ?? appInfo.AppName);
            var arguments = new List<string>();

            if (!File.Exists(binaryPath))
            {
                _mainLog.WriteLine($"Failed to find an executable binary at {binaryPath}. Trying to run app using `open -W`");
                binaryPath = "open";
                arguments.Add("-W");
                arguments.Add(appInfo.LaunchAppPath);
            }

            arguments.AddRange(appArguments);

            var envVars = environmentVariables.ToDictionary(
                p => p.Key,
                p => p.Value is bool ? p.Value.ToString().ToLowerInvariant() : p.Value.ToString()); // turn True to true

            return _processManager.ExecuteCommandAsync(binaryPath, arguments, _mainLog, timeout, envVars, cancellationToken);
        }
    }
}
