// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.AndroidHeadless;

internal class AndroidHeadlessInstallCommandArguments : XHarnessCommandArguments, IAndroidHeadlessAppRunArguments
{
    public TestAppPathArgument TestAppPath { get; } = new();
    public TestAppCommandArgument TestAppCommand { get; } = new();
    public OutputDirectoryArgument OutputDirectory { get; } = new();
    public TimeoutArgument Timeout { get; } = new(TimeSpan.FromMinutes(15));
    public LaunchTimeoutArgument LaunchTimeout { get; } = new(TimeSpan.FromMinutes(5));
    public DeviceIdArgument DeviceId { get; } = new();
    public DeviceArchitectureArgument DeviceArchitecture { get; } = new();
    public ApiVersionArgument ApiVersion { get; } = new();

    protected override IEnumerable<Argument> GetArguments() => new Argument[]
    {
        TestAppPath,
        OutputDirectory,
        Timeout,
        DeviceId,
        LaunchTimeout,
        DeviceArchitecture,
        ApiVersion,
    };
}
