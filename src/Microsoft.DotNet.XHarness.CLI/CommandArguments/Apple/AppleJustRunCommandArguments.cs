// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.XHarness.Common.CLI;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple
{
    internal class AppleJustRunCommandArguments : XHarnessCommandArguments, IAppleAppRunArguments
    {
        public BundleIdentifierArgument BundleIdentifier { get; } = new();
        public TargetArgument Target { get; } = new();
        public OutputDirectoryArgument OutputDirectory { get; } = new();
        public TimeoutArgument Timeout { get; } = new(TimeSpan.FromMinutes(15));
        public XcodeArgument XcodeRoot { get; } = new();
        public MlaunchArgument MlaunchPath { get; } = new();
        public DeviceNameArgument DeviceName { get; } = new();
        public IncludeWirelessArgument IncludeWireless { get; } = new();
        public EnableLldbArgument EnableLldb { get; } = new();
        public EnvironmentalVariablesArgument EnvironmentalVariables { get; } = new();
        public ExpectedExitCodeArgument ExpectedExitCode { get; } = new((int)ExitCode.SUCCESS);
        public SignalAppEndArgument SignalAppEnd { get; } = new();

        protected override IEnumerable<Argument> GetArguments() => new Argument[]
        {
            BundleIdentifier,
            Target,
            OutputDirectory,
            DeviceName,
            IncludeWireless,
            Timeout,
            ExpectedExitCode,
            XcodeRoot,
            MlaunchPath,
            EnableLldb,
            SignalAppEnd,
            EnvironmentalVariables,
        };
    }
}
