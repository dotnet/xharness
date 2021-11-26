// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;

internal class AndroidInstallCommandArguments : XHarnessCommandArguments
{
    public AppPathArgument AppPackagePath { get; } = new();
    public PackageNameArgument PackageName { get; } = new();
    public OutputDirectoryArgument OutputDirectory { get; } = new();
    public TimeoutArgument Timeout { get; } = new(TimeSpan.FromMinutes(15));
    public DeviceIdArgument DeviceId { get; } = new();
    public LaunchTimeoutArgument LaunchTimeout { get; } = new(TimeSpan.FromMinutes(5));
    public DeviceArchitectureArgument DeviceArchitecture { get; } = new();

    protected override IEnumerable<Argument> GetArguments() => new Argument[]
    {
            AppPackagePath,
            PackageName,
            OutputDirectory,
            Timeout,
            DeviceId,
            LaunchTimeout,
            DeviceArchitecture,
    };
}
