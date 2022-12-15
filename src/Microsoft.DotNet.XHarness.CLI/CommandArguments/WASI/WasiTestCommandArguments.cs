// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasi;

internal class WasiTestCommandArguments : XHarnessCommandArguments
{
    public WasmEngineArgument Engine { get; } = new();
    public WasmEngineLocationArgument EnginePath { get; } = new();
    public WasmEngineArguments EngineArgs { get; } = new();
    public ExpectedExitCodeArgument ExpectedExitCode { get; } = new((int)Common.CLI.ExitCode.SUCCESS);
    public OutputDirectoryArgument OutputDirectory { get; } = new();
    public TimeoutArgument Timeout { get; } = new(TimeSpan.FromMinutes(15));

    protected override IEnumerable<Argument> GetArguments() => new Argument[]
    {
            Engine,
            EnginePath,
            EngineArgs,
            OutputDirectory,
            Timeout,
            ExpectedExitCode,
    };
}
