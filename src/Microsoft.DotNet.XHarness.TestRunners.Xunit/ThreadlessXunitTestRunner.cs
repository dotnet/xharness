// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.TestRunners.Common;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
using ExceptionUtility = Xunit.ExceptionUtility;

#nullable enable
namespace Microsoft.DotNet.XHarness.TestRunners.Xunit;

internal class ThreadlessXunitTestRunner : XunitTestRunnerBase
{
    public ThreadlessXunitTestRunner(LogWriter logger, bool oneLineResults = false) : base(logger)
    {
        _oneLineResults = oneLineResults;
    }

    protected override string ResultsFileName { get => string.Empty; set => throw new InvalidOperationException("This runner outputs its results to stdout."); }

    private readonly XElement _assembliesElement = new XElement("assemblies");
    private readonly bool _oneLineResults;

    public override async Task Run(IEnumerable<TestAssemblyInfo> testAssemblies)
    {
        var configuration = new TestAssemblyConfiguration() { ShadowCopy = false, ParallelizeAssembly = false, ParallelizeTestCollections = false, MaxParallelThreads = 1, PreEnumerateTheories = false };
        var discoveryOptions = TestFrameworkOptions.ForDiscovery(configuration);
        var discoverySink = new TestDiscoverySink();
        var diagnosticSink = new ConsoleDiagnosticMessageSink();
        var testOptions = TestFrameworkOptions.ForExecution(configuration);
        var testSink = new TestMessageSink();

        var totalSummary = new ExecutionSummary();
        foreach (var testAsmInfo in testAssemblies)
        {
            string assemblyFileName = testAsmInfo.FullPath;
            var controller = new ThreadlessXunit2(AppDomainSupport.Denied, new NullSourceInformationProvider(), assemblyFileName, configFileName: null, shadowCopy: false, shadowCopyFolder: null, diagnosticMessageSink: diagnosticSink, verifyTestAssemblyExists: false);

            discoveryOptions.SetSynchronousMessageReporting(true);
            testOptions.SetSynchronousMessageReporting(true);

            Console.WriteLine($"Discovering: {assemblyFileName} (method display = {discoveryOptions.GetMethodDisplayOrDefault()}, method display options = {discoveryOptions.GetMethodDisplayOptionsOrDefault()})");
            var assemblyInfo = new global::Xunit.Sdk.ReflectionAssemblyInfo(testAsmInfo.Assembly);
            var discoverer = new ThreadlessXunitDiscoverer(assemblyInfo, new NullSourceInformationProvider(), discoverySink);

            discoverer.FindWithoutThreads(includeSourceInformation: false, discoverySink, discoveryOptions);
            var testCasesToRun = discoverySink.TestCases.Where(t => !_filters.IsExcluded(t)).ToList();
            Console.WriteLine($"Discovered:  {assemblyFileName} (found {testCasesToRun.Count} of {discoverySink.TestCases.Count} test cases)");

            var summaryTaskSource = new TaskCompletionSource<ExecutionSummary>();
            var resultsXmlAssembly = new XElement("assembly");
            var resultsSink = new DelegatingXmlCreationSink(new DelegatingExecutionSummarySink(testSink), resultsXmlAssembly);
            var completionSink = new CompletionCallbackExecutionSink(resultsSink, summary => summaryTaskSource.SetResult(summary));

            if (Environment.GetEnvironmentVariable("XHARNESS_LOG_TEST_START") != null)
            {
                testSink.Execution.TestStartingEvent += args => { Console.WriteLine($"[STRT] {args.Message.Test.DisplayName}"); };
            }
            testSink.Execution.TestPassedEvent += args =>
            {
                Console.WriteLine($"[PASS] {EscapeNewLines(args.Message.Test.DisplayName)}");
                PassedTests++;
            };
            testSink.Execution.TestSkippedEvent += args =>
            {
                Console.WriteLine($"[SKIP] {EscapeNewLines(args.Message.Test.DisplayName)}");
                SkippedTests++;
            };
            testSink.Execution.TestFailedEvent += args =>
            {
                Console.WriteLine($"[FAIL] {EscapeNewLines(args.Message.Test.DisplayName)}{Environment.NewLine}{ExceptionUtility.CombineMessages(args.Message)}{Environment.NewLine}{ExceptionUtility.CombineStackTraces(args.Message)}");
                FailedTests++;
            };
            testSink.Execution.TestFinishedEvent += args => ExecutedTests++;

            testSink.Execution.TestAssemblyStartingEvent += args => { Console.WriteLine($"Starting:    {assemblyFileName}"); };
            testSink.Execution.TestAssemblyFinishedEvent += args => { Console.WriteLine($"Finished:    {assemblyFileName}"); };

            controller.RunTests(testCasesToRun, completionSink, testOptions);

            totalSummary = Combine(totalSummary, await summaryTaskSource.Task);

            _assembliesElement.Add(resultsXmlAssembly);
        }
        TotalTests = totalSummary.Total;
        Console.WriteLine($"{Environment.NewLine}=== TEST EXECUTION SUMMARY ==={Environment.NewLine}Total: {totalSummary.Total}, Errors: 0, Failed: {totalSummary.Failed}, Skipped: {totalSummary.Skipped}, Time: {TimeSpan.FromSeconds((double)totalSummary.Time).TotalSeconds}s{Environment.NewLine}");

        static string EscapeNewLines(string message) => message.Replace("\r", "\\r").Replace("\n", "\\n");
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

    public override string WriteResultsToFile(XmlResultJargon xmlResultJargon)
    {
        Debug.Assert(xmlResultJargon == XmlResultJargon.xUnit);
        WriteResultsToFile(Console.Out, xmlResultJargon);
        return "";
    }

    public override void WriteResultsToFile(TextWriter writer, XmlResultJargon jargon)
    {
        if (_oneLineResults)
        {
            using var ms = new MemoryStream();
            _assembliesElement.Save(ms);
            ms.TryGetBuffer(out var bytes);
            var base64 = Convert.ToBase64String(bytes, Base64FormattingOptions.None);
            Console.WriteLine($"STARTRESULTXML {bytes.Count} {base64} ENDRESULTXML");
            Console.WriteLine($"Finished writing {bytes.Count} bytes of RESULTXML");
        }
        else
        {
            writer.WriteLine($"STARTRESULTXML");
            _assembliesElement.Save(writer);
            writer.WriteLine();
            writer.WriteLine($"ENDRESULTXML");
        }
    }
}

internal class ThreadlessXunitDiscoverer : global::Xunit.Sdk.XunitTestFrameworkDiscoverer
{
    public ThreadlessXunitDiscoverer(IAssemblyInfo assemblyInfo, ISourceInformationProvider sourceProvider, IMessageSink diagnosticMessageSink)
        : base(assemblyInfo, sourceProvider, diagnosticMessageSink)
    {
    }

    public void FindWithoutThreads(bool includeSourceInformation, IMessageSink discoveryMessageSink, ITestFrameworkDiscoveryOptions discoveryOptions)
    {
        using (var messageBus = new global::Xunit.Sdk.SynchronousMessageBus(discoveryMessageSink))
        {
            foreach (var type in AssemblyInfo.GetTypes(includePrivateTypes: false).Where(IsValidTestClass))
            {
                var testClass = CreateTestClass(type);
                if (!FindTestsForType(testClass, includeSourceInformation, messageBus, discoveryOptions))
                {
                    break;
                }
            }

            messageBus.QueueMessage(new global::Xunit.Sdk.DiscoveryCompleteMessage());
        }
    }
}

internal class ConsoleDiagnosticMessageSink : global::Xunit.Sdk.LongLivedMarshalByRefObject, IMessageSink
{
    public bool OnMessage(IMessageSinkMessage message)
    {
        if (message is IDiagnosticMessage diagnosticMessage)
        {
            Console.WriteLine(diagnosticMessage.Message);
        }

        return true;
    }
}

public class ThreadlessXunitTestMethodRunner : XunitTestMethodRunner
{
    public ThreadlessXunitTestMethodRunner(ITestMethod testMethod, IReflectionTypeInfo @class, IReflectionMethodInfo method, IEnumerable<IXunitTestCase> testCases, IMessageSink diagnosticMessageSink, IMessageBus messageBus, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource, object[] constructorArguments)
        : base(testMethod, @class, method, testCases, diagnosticMessageSink, messageBus, aggregator, cancellationTokenSource, constructorArguments)
    {
    }

    protected override async Task<RunSummary> RunTestCaseAsync(IXunitTestCase testCase)
    {
        await Task.Yield();
        return await base.RunTestCaseAsync(testCase);
    }

}

public class ThreadlessXunitTestClassRunner : XunitTestClassRunner
{
    public ThreadlessXunitTestClassRunner(ITestClass testClass, IReflectionTypeInfo @class, IEnumerable<IXunitTestCase> testCases, IMessageSink diagnosticMessageSink, IMessageBus messageBus, ITestCaseOrderer testCaseOrderer, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource, IDictionary<Type, object> collectionFixtureMappings)
        : base(testClass, @class, testCases, diagnosticMessageSink, messageBus, testCaseOrderer, aggregator, cancellationTokenSource, collectionFixtureMappings)
    {
    }

    protected override async Task AfterTestClassStartingAsync()
    {
        await Task.Delay(10);
        await base.AfterTestClassStartingAsync();
    }

    protected override async Task<RunSummary> RunTestMethodAsync(ITestMethod testMethod, IReflectionMethodInfo method, IEnumerable<IXunitTestCase> testCases, object[] constructorArguments)
    {
        var runner = new ThreadlessXunitTestMethodRunner(testMethod, Class, method, testCases, DiagnosticMessageSink, MessageBus, new ExceptionAggregator(Aggregator), CancellationTokenSource, constructorArguments);
        return await runner.RunAsync();
    }
}

public class ThreadlessXunitTestCollectionRunner : XunitTestCollectionRunner
{
    public ThreadlessXunitTestCollectionRunner(ITestCollection testCollection, IEnumerable<IXunitTestCase> testCases, IMessageSink diagnosticMessageSink, IMessageBus messageBus, ITestCaseOrderer testCaseOrderer, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
        : base(testCollection, testCases, diagnosticMessageSink, messageBus, testCaseOrderer, aggregator, cancellationTokenSource)
    {
    }

    protected override async Task AfterTestCollectionStartingAsync()
    {
        await Task.Yield();
        await base.AfterTestCollectionStartingAsync();
    }

    protected override async Task<RunSummary> RunTestClassAsync(ITestClass testClass, IReflectionTypeInfo @class, IEnumerable<IXunitTestCase> testCases)
    {
        var runner = new ThreadlessXunitTestClassRunner(testClass, @class, testCases, DiagnosticMessageSink, MessageBus, TestCaseOrderer, new ExceptionAggregator(Aggregator), CancellationTokenSource, CollectionFixtureMappings);
        return await runner.RunAsync();
    }
}

public class ThreadlessXunitTestAssemblyRunner : XunitTestAssemblyRunner
{
    public ThreadlessXunitTestAssemblyRunner(ITestAssembly testAssembly, IEnumerable<IXunitTestCase> testCases, IMessageSink diagnosticMessageSink, IMessageSink executionMessageSink, ITestFrameworkExecutionOptions executionOptions)
        : base(testAssembly, testCases, diagnosticMessageSink, executionMessageSink, executionOptions)
    {
    }

    protected override async Task AfterTestAssemblyStartingAsync()
    {
        await Task.Yield();
        await base.AfterTestAssemblyStartingAsync();
    }

    protected override async Task<RunSummary> RunTestCollectionAsync(IMessageBus messageBus, ITestCollection testCollection, IEnumerable<IXunitTestCase> testCases, CancellationTokenSource cancellationTokenSource)
    {
        var runner = new ThreadlessXunitTestCollectionRunner(testCollection, testCases, DiagnosticMessageSink, messageBus, TestCaseOrderer, new ExceptionAggregator(Aggregator), cancellationTokenSource);
        return await runner.RunAsync();
    }
}

public class ThreadlessXunitTestFrameworkExecutor : XunitTestFrameworkExecutor
{
    public ThreadlessXunitTestFrameworkExecutor(AssemblyName assemblyName, ISourceInformationProvider sourceInformationProvider, IMessageSink diagnosticMessageSink)
        : base(assemblyName, sourceInformationProvider, diagnosticMessageSink)
    {
    }

    protected override async void RunTestCases(IEnumerable<IXunitTestCase> testCases, IMessageSink executionMessageSink, ITestFrameworkExecutionOptions executionOptions)
    {
        using var assemblyRunner = new ThreadlessXunitTestAssemblyRunner(TestAssembly, testCases, DiagnosticMessageSink, executionMessageSink, executionOptions);
        await Task.Delay(10);
        await assemblyRunner.RunAsync();
    }
}

#pragma warning disable xUnit3000 // Classes which cross AppDomain boundaries must derive directly or indirectly from LongLivedMarshalByRefObject
public class ThreadlessXunit2 : Xunit2, IFrontController, ITestCaseBulkDeserializer
#pragma warning restore xUnit3000 // Classes which cross AppDomain boundaries must derive directly or indirectly from LongLivedMarshalByRefObject
{
    public ThreadlessXunit2(AppDomainSupport appDomainSupport, ISourceInformationProvider sourceInformationProvider, string assemblyFileName, string? configFileName = null, bool shadowCopy = true, string? shadowCopyFolder = null, IMessageSink? diagnosticMessageSink = null, bool verifyTestAssemblyExists = true)
        : base(appDomainSupport, sourceInformationProvider, assemblyFileName, configFileName, shadowCopy, shadowCopyFolder, diagnosticMessageSink, verifyTestAssemblyExists)
    {
        var reField = typeof(Xunit2).GetField("remoteExecutor", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var an = Assembly.Load(new AssemblyName { Name = Path.GetFileNameWithoutExtension(assemblyFileName) }).GetName();
        var assemblyName = new AssemblyName { Name = an.Name, Version = an.Version };
        var replace = new ThreadlessXunitTestFrameworkExecutor(assemblyName, sourceInformationProvider, diagnosticMessageSink!);
        reField.SetValue(this, replace);
    }
}
