// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.XHarness.TestRunners.Xunit;

internal class ThreadlessXunitTestRunner
{
    public static async Task<int> Run(string assemblyFileName, bool printXml, XunitFilters filters, bool oneLineResults = false)
    {
        try
        {
            var configuration = new TestAssemblyConfiguration() { ShadowCopy = false, ParallelizeAssembly = false, ParallelizeTestCollections = false, MaxParallelThreads = 1, PreEnumerateTheories = false };
            var discoveryOptions = TestFrameworkOptions.ForDiscovery(configuration);
            var discoverySink = new TestDiscoverySink();
            var diagnosticSink = new ConsoleDiagnosticMessageSink();
            var testOptions = TestFrameworkOptions.ForExecution(configuration);
            var testSink = new TestMessageSink();
            var controller = new Xunit2(AppDomainSupport.Denied, new NullSourceInformationProvider(), assemblyFileName, configFileName: null, shadowCopy: false, shadowCopyFolder: null, diagnosticMessageSink: diagnosticSink, verifyTestAssemblyExists: false);

            discoveryOptions.SetSynchronousMessageReporting(true);
            testOptions.SetSynchronousMessageReporting(true);

            Console.WriteLine($"Discovering: {assemblyFileName} (method display = {discoveryOptions.GetMethodDisplayOrDefault()}, method display options = {discoveryOptions.GetMethodDisplayOptionsOrDefault()})");
            var assembly = Assembly.LoadFrom(assemblyFileName);
            var assemblyInfo = new global::Xunit.Sdk.ReflectionAssemblyInfo(assembly);
            var discoverer = new ThreadlessXunitDiscoverer(assemblyInfo, new NullSourceInformationProvider(), discoverySink);

            discoverer.FindWithoutThreads(includeSourceInformation: false, discoverySink, discoveryOptions);
            var testCasesToRun = discoverySink.TestCases.Where(filters.Filter).ToList();
            Console.WriteLine($"Discovered:  {assemblyFileName} (found {testCasesToRun.Count} of {discoverySink.TestCases.Count} test cases)");

            var summaryTaskSource = new TaskCompletionSource<ExecutionSummary>();
            var summarySink = new DelegatingExecutionSummarySink(testSink, () => false, (completed, summary) => summaryTaskSource.SetResult(summary));
            var resultsXmlAssembly = new XElement("assembly");
            var resultsSink = new DelegatingXmlCreationSink(summarySink, resultsXmlAssembly);

            if (Environment.GetEnvironmentVariable("XHARNESS_LOG_TEST_START") != null)
            {
                testSink.Execution.TestStartingEvent += args => { Console.WriteLine($"[STRT] {args.Message.Test.DisplayName}"); };
            }
            testSink.Execution.TestPassedEvent += args => { Console.WriteLine($"[PASS] {args.Message.Test.DisplayName}"); };
            testSink.Execution.TestSkippedEvent += args => { Console.WriteLine($"[SKIP] {args.Message.Test.DisplayName}"); };
            testSink.Execution.TestFailedEvent += args => { Console.WriteLine($"[FAIL] {args.Message.Test.DisplayName}{Environment.NewLine}{ExceptionUtility.CombineMessages(args.Message)}{Environment.NewLine}{ExceptionUtility.CombineStackTraces(args.Message)}"); };

            testSink.Execution.TestAssemblyStartingEvent += args => { Console.WriteLine($"Starting:    {assemblyFileName}"); };
            testSink.Execution.TestAssemblyFinishedEvent += args => { Console.WriteLine($"Finished:    {assemblyFileName}"); };

            controller.RunTests(testCasesToRun, resultsSink, testOptions);

            var summary = await summaryTaskSource.Task;
            Console.WriteLine($"{Environment.NewLine}=== TEST EXECUTION SUMMARY ==={Environment.NewLine}Total: {summary.Total}, Errors: 0, Failed: {summary.Failed}, Skipped: {summary.Skipped}, Time: {TimeSpan.FromSeconds((double)summary.Time).TotalSeconds}s{Environment.NewLine}");

            if (printXml)
            {
                if (oneLineResults)
                {
                    var resultsXml = new XElement("assemblies");
                    resultsXml.Add(resultsXmlAssembly);
                    using var ms = new MemoryStream();
                    resultsXml.Save(ms);
                    var bytes = ms.ToArray();
                    var base64 = Convert.ToBase64String(bytes, Base64FormattingOptions.None);
                    Console.WriteLine($"STARTRESULTXML {bytes.Length} {base64} ENDRESULTXML");
                    Console.WriteLine($"Finished writing {bytes.Length} bytes of RESULTXML");
                }
                else
                {
                    Console.WriteLine($"STARTRESULTXML");
                    var resultsXml = new XElement("assemblies");
                    resultsXml.Add(resultsXmlAssembly);
                    resultsXml.Save(Console.Out);
                    Console.WriteLine();
                    Console.WriteLine($"ENDRESULTXML");
                }
            }

            var failed = resultsSink.ExecutionSummary.Failed > 0 || resultsSink.ExecutionSummary.Errors > 0;
            return failed ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ThreadlessXunitTestRunner failed: {ex}");
            return 2;
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
