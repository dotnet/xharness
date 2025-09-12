// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.AndroidHeadless;

internal class AndroidHeadlessTestCommandArguments : XHarnessCommandArguments, IAndroidHeadlessAppRunArguments
{
    public TestPathArgument TestPath { get; } = new();
    public RuntimePathArgument RuntimePath { get; } = new();
    public TestAssemblyArgument TestAssembly { get; } = new();
    public TestScriptArgument TestScript { get; } = new();
    public OutputDirectoryArgument OutputDirectory { get; } = new();
    public TimeoutArgument Timeout { get; } = new(TimeSpan.FromMinutes(15));
    public LaunchTimeoutArgument LaunchTimeout { get; } = new(TimeSpan.FromMinutes(5));
    public DeviceIdArgument DeviceId { get; } = new();
    public DeviceArchitectureArgument DeviceArchitecture { get; } = new();
    public ApiVersionArgument ApiVersion { get; } = new();
    public ApiLevelsArgument ApiLevels { get; } = new();
    public ExpectedExitCodeArgument ExpectedExitCode { get; } = new((int)Common.CLI.ExitCode.SUCCESS);
    public WifiArgument Wifi { get; } = new();

    protected override IEnumerable<Argument> GetArguments() => new Argument[]
    {
        TestPath,
        RuntimePath,
        TestAssembly,
        TestScript,
        OutputDirectory,
        Timeout,
        LaunchTimeout,
        DeviceArchitecture,
        DeviceId,
        ApiVersion,
        ApiLevels,
        ExpectedExitCode,
        Wifi,
    };

    public override void Validate()
    {
        base.Validate();

        // Validate that both ApiVersion and ApiLevels are not specified at the same time
        if (ApiVersion.Value.HasValue && ApiLevels.Value.Any())
        {
            throw new ArgumentException("Cannot specify both --api-version and --api-levels. Use --api-levels for multiple API levels or --api-version for a single API level.");
        }
    }
}
