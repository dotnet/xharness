// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.TestRunners.Common;

#nullable enable
namespace Microsoft.DotNet.XHarness.TestRunners.Xunit.v3;

public abstract class WasmApplicationEntryPoint : WasmApplicationEntryPointBase
{
    protected override TestRunner GetTestRunner(LogWriter logWriter)
    {
        var runner = new XunitV3TestRunner(logWriter);
        ConfigureRunnerFilters(runner, ApplicationOptions.Current);
        return runner;
    }

    protected override bool IsXunit => true;
}