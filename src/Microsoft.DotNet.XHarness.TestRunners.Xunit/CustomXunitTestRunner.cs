// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.DotNet.XHarness.TestRunners.Common;
using Xunit;
using Xunit.Abstractions;

#nullable enable
namespace Microsoft.DotNet.XHarness.TestRunners.Xunit;

/// <summary>
/// Abstract xunit test runner that uses reflection-based discovery (NativeAOT-safe).
/// Concrete implementations define configuration (parallelism) and result output behavior.
/// </summary>
internal abstract class CustomXunitTestRunner : XunitTestRunnerBase
{
    protected CustomXunitTestRunner(LogWriter logger) : base(logger)
    {
        ShowFailureInfos = false;
    }

    protected abstract string RunnerDisplayName { get; }

    private protected XElement? _assembliesElement;

    internal XElement ConsumeAssembliesElement()
    {
        Debug.Assert(_assembliesElement != null, "ConsumeAssembliesElement called before Run() or after ConsumeAssembliesElement() was already called.");
        var res = _assembliesElement;
        _assembliesElement = null;
        FailureInfos.Clear();
        return res!;
    }

    protected abstract TestAssemblyConfiguration CreateConfiguration();

    public override async Task Run(IEnumerable<TestAssemblyInfo> testAssemblies)
    {
        OnInfo($"Using {RunnerDisplayName}");

        _assembliesElement = new XElement("assemblies");

        var configuration = CreateConfiguration();
        var discoveryOptions = TestFrameworkOptions.ForDiscovery(configuration);
        var discoverySink = new TestDiscoverySink();
        var diagnosticSink = new ConsoleDiagnosticMessageSink(Logger);
        var testOptions = TestFrameworkOptions.ForExecution(configuration);
        var testSink = new TestMessageSink();

        var totalSummary = new ExecutionSummary();
        foreach (var testAsmInfo in testAssemblies)
        {
            string assemblyFileName = testAsmInfo.FullPath;
            var controller = new YieldingXunit2(AppDomainSupport.Denied, new NullSourceInformationProvider(), assemblyFileName, configFileName: null, shadowCopy: false, shadowCopyFolder: null, diagnosticMessageSink: diagnosticSink, verifyTestAssemblyExists: false);

            discoveryOptions.SetSynchronousMessageReporting(true);
            testOptions.SetSynchronousMessageReporting(true);

            OnInfo($"Discovering: {assemblyFileName} (method display = {discoveryOptions.GetMethodDisplayOrDefault()}, method display options = {discoveryOptions.GetMethodDisplayOptionsOrDefault()})");
            var assemblyInfo = new global::Xunit.Sdk.ReflectionAssemblyInfo(testAsmInfo.Assembly);
            var discoverer = new ThreadlessXunitDiscoverer(assemblyInfo, new NullSourceInformationProvider(), discoverySink);

            discoverer.FindWithoutThreads(includeSourceInformation: false, discoverySink, discoveryOptions);
            var testCasesToRun = discoverySink.TestCases.Where(t => !_filters.IsExcluded(t)).ToList();
            OnInfo($"Discovered:  {assemblyFileName} (found {testCasesToRun.Count} of {discoverySink.TestCases.Count} test cases)");

            var summaryTaskSource = new TaskCompletionSource<ExecutionSummary>();
            var resultsXmlAssembly = new XElement("assembly");
#pragma warning disable CS0618 // Delegating*Sink types are marked obsolete, but we can't move to ExecutionSink yet: https://github.com/dotnet/arcade/issues/14375
            var resultsSink = new DelegatingXmlCreationSink(new DelegatingExecutionSummarySink(testSink), resultsXmlAssembly);
#pragma warning restore
            var completionSink = new CompletionCallbackExecutionSink(resultsSink, summary => summaryTaskSource.SetResult(summary));

            if (EnvironmentVariables.IsLogTestStart())
            {
                testSink.Execution.TestStartingEvent += args => { OnInfo($"[STRT] {args.Message.Test.DisplayName}"); };
            }
            testSink.Execution.TestPassedEvent += args =>
            {
                OnDebug($"[PASS] {args.Message.Test.DisplayName}");
                PassedTests++;
            };
            testSink.Execution.TestSkippedEvent += args =>
            {
                OnDebug($"[SKIP] {args.Message.Test.DisplayName}");
                SkippedTests++;
            };
            testSink.Execution.TestFailedEvent += args =>
            {
                OnError($"[FAIL] {args.Message.Test.DisplayName}{Environment.NewLine}{ExceptionUtility.CombineMessages(args.Message)}{Environment.NewLine}{ExceptionUtility.CombineStackTraces(args.Message)}");
                FailedTests++;
            };
            testSink.Execution.TestFinishedEvent += args => ExecutedTests++;

            testSink.Execution.TestAssemblyStartingEvent += args => { Console.WriteLine($"Starting:    {assemblyFileName}"); };
            testSink.Execution.TestAssemblyFinishedEvent += args => { Console.WriteLine($"Finished:    {assemblyFileName}"); };

            await controller.RunTestsAsync(testCasesToRun, MessageSinkAdapter.Wrap(completionSink), testOptions);

            totalSummary = Combine(totalSummary, await summaryTaskSource.Task);

            _assembliesElement.Add(resultsXmlAssembly);
        }
        TotalTests = totalSummary.Total;
    }

    private ExecutionSummary Combine(ExecutionSummary aggregateSummary, ExecutionSummary assemblySummary)
    {
        return new ExecutionSummary
        {
            Total = aggregateSummary.Total + assemblySummary.Total,
            Failed = aggregateSummary.Failed + assemblySummary.Failed,
            Skipped = aggregateSummary.Skipped + assemblySummary.Skipped,
            Errors = aggregateSummary.Errors + assemblySummary.Errors,
            Time = aggregateSummary.Time + assemblySummary.Time
        };
    }
}
