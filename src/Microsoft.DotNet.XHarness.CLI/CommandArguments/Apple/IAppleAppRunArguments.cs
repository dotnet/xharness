﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple
{
    internal interface IAppleAppRunArguments
    {
        public TargetArgument Target { get; }
        public OutputDirectoryArgument OutputDirectory { get; }
        public TimeoutArgument Timeout { get; }
        public XcodeArgument XcodeRoot { get; }
        public MlaunchArgument Mlaunch { get; }
        public DeviceNameArgument DeviceName { get; }
        public EnableLldbArgument EnableLldb { get; }
        public EnvironmentalVariablesArgument EnvironmentalVariables { get; }
        public ResetSimulatorArgument ResetSimulator { get; }
    }
}
