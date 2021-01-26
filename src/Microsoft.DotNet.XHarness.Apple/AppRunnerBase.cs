// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;

namespace Microsoft.DotNet.XHarness.Apple
{
    public abstract class AppRunnerBase
    {
        protected readonly IFileBackedLog _mainLog;
        private readonly IHardwareDeviceLoader _hardwareDeviceLoader;

        protected AppRunnerBase(
            IHardwareDeviceLoader hardwareDeviceLoader,
            IFileBackedLog mainLog,
            Action<string>? logCallback = null)
        {
            _hardwareDeviceLoader = hardwareDeviceLoader ?? throw new ArgumentNullException(nameof(hardwareDeviceLoader));

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
    }
}
