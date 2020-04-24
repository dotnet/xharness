// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;

namespace Microsoft.DotNet.XHarness.iOS
{
    public interface IDeviceLogCapturerFactory
    {
        IDeviceLogCapturer Create(ILog mainLog, ILog deviceLog, string deviceName);
    }

    public class DeviceLogCapturerFactory : IDeviceLogCapturerFactory
    {
        readonly IProcessManager processManager;

        public DeviceLogCapturerFactory(IProcessManager processManager)
        {
            this.processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        }

        public IDeviceLogCapturer Create(ILog mainLog, ILog deviceLog, string deviceName)
        {
            return new DeviceLogCapturer(processManager, mainLog, deviceLog, deviceName);
        }
    }
}

