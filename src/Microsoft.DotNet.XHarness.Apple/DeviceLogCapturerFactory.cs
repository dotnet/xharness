// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;

namespace Microsoft.DotNet.XHarness.Apple;

public interface IDeviceLogCapturerFactory
{
    IDeviceLogCapturer Create(ILog mainLog, ILog deviceLog, string deviceUdid);
}

public class DeviceLogCapturerFactory : IDeviceLogCapturerFactory
{
    public IDeviceLogCapturer Create(ILog mainLog, ILog deviceLog, string deviceUdid) => new DeviceLogCapturer(mainLog, deviceLog, deviceUdid);
}

