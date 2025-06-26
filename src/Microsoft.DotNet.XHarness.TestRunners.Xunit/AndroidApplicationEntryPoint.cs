// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.TestRunners.Common;

#nullable enable
namespace Microsoft.DotNet.XHarness.TestRunners.Xunit;

public abstract class AndroidApplicationEntryPoint : AndroidApplicationEntryPointBase
{
    protected override bool IsXunit => true;

    protected override TestRunner GetTestRunner(LogWriter logWriter)
    {
#if USE_XUNIT_V3
        var runner = new XUnitTestRunner(logWriter);
#else
        var runner = new XUnitTestRunner(logWriter) { MaxParallelThreads = MaxParallelThreads };
#endif
        ConfigureRunnerFilters(runner, ApplicationOptions.Current);
        return runner;
    }
}
