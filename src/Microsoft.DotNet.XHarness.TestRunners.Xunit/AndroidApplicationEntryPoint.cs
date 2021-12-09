#nullable enable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.TestRunners.Common;

namespace Microsoft.DotNet.XHarness.TestRunners.Xunit;

public abstract class AndroidApplicationEntryPoint : AndroidApplicationEntryPointBase
{
    protected override bool IsXunit => true;

    protected override TestRunner GetTestRunner(LogWriter logWriter)
    {
        var runner = new XUnitTestRunner(logWriter) { MaxParallelThreads = MaxParallelThreads };
        ConfigureRunnerFilters(runner, ApplicationOptions.Current);
        return runner;
    }
}
