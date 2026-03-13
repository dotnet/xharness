// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using Microsoft.DotNet.XHarness.TestRunners.Common;

#nullable enable
namespace Microsoft.DotNet.XHarness.TestRunners.Xunit;

public abstract class iOSApplicationEntryPoint : iOSApplicationEntryPointBase
{
    protected override TestRunner GetTestRunner(LogWriter logWriter)
    {
        if (!RuntimeFeature.IsDynamicCodeSupported)
        {
            var nativeAotRunner = new NativeAotXunitTestRunner(logWriter) { MaxParallelThreads = MaxParallelThreads };
            ConfigureRunnerFilters(nativeAotRunner, ApplicationOptions.Current);
            return nativeAotRunner;
        }

        var runner = new XUnitTestRunner(logWriter) { MaxParallelThreads = MaxParallelThreads };
        ConfigureRunnerFilters(runner, ApplicationOptions.Current);
        return runner;
    }

    protected override bool IsXunit => true;
}
