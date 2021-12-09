// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.TestRunners.Common;
using Xunit;

namespace Microsoft.DotNet.XHarness.TestRunners.Xunit;

public abstract class WasmApplicationEntryPoint : WasmApplicationEntryPointBase
{
    protected virtual string TestAssembly { get; set; } = "";
    protected virtual IEnumerable<string> ExcludedTraits { get; set; } = Array.Empty<string>();
    protected virtual IEnumerable<string> IncludedTraits { get; set; } = Array.Empty<string>();
    protected virtual IEnumerable<string> IncludedClasses { get; set; } = Array.Empty<string>();
    protected virtual IEnumerable<string> IncludedMethods { get; set; } = Array.Empty<string>();
    protected virtual IEnumerable<string> IncludedNamespaces { get; set; } = Array.Empty<string>();

    protected override bool IsXunit => true;

    protected override TestRunner GetTestRunner(LogWriter logWriter)
    {
        var runner = new ThreadlessXunitTestRunner(logWriter, true);
        ConfigureRunnerFilters(runner, ApplicationOptions.Current);

        runner.SkipCategories(ExcludedTraits);
        runner.SkipCategories(IncludedTraits, isExcluded: false);
        foreach (var cls in IncludedClasses)
        {
            runner.SkipClass(cls, false);
        }
        foreach (var method in IncludedMethods)
        {
            runner.SkipMethod(method, false);
        }
        foreach (var ns in IncludedNamespaces)
        {
            runner.SkipNamespace(ns, isExcluded: false);
        }
        return runner;
    }

    protected override IEnumerable<TestAssemblyInfo> GetTestAssemblies()
        => new[] { new TestAssemblyInfo(Assembly.LoadFrom(TestAssembly), TestAssembly) };

    public async Task<int> Run()
    {
        await RunAsync();

        return LastRunHadFailedTests ? 1 : 0;
    }
}
