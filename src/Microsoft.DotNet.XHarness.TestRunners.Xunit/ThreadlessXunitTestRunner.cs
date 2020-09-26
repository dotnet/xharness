// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Xml.Linq;

using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.XHarness.TestRunners.Xunit
{
    internal class ThreadlessXunitTestRunner
    {
        public int Run(string assemblyFileName, bool printXml, XunitFilters filters)
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

            Console.WriteLine($"Discovering tests for {assemblyFileName}");
            var assembly = Assembly.LoadFrom(assemblyFileName);
            var assemblyInfo = new global::Xunit.Sdk.ReflectionAssemblyInfo(assembly);
            var discoverer = new ThreadlessXunitDiscoverer(assemblyInfo, new NullSourceInformationProvider(), discoverySink);

            discoverer.FindWithoutThreads(includeSourceInformation: false, discoverySink, discoveryOptions);
            discoverySink.Finished.WaitOne();
            var testCasesToRun = discoverySink.TestCases.Where(filters.Filter).ToList();
            Console.WriteLine($"Discovery finished.");

            var delegatingSummarySink = new DelegatingExecutionSummarySink(testSink, () => false, null);
            var summarySink = new WasmExecutionSummarySink(delegatingSummarySink, () => false, (completed, summary) => { Console.WriteLine($"Tests run: {summary.Total}, Errors: 0, Failures: {summary.Failed}, PNSE: {summary.PNSE}, Skipped: {summary.Skipped}. Time: {TimeSpan.FromSeconds((double)summary.Time).TotalSeconds}s"); });
            var resultsXmlAssembly = new XElement("assembly");
            var resultsSink = new DelegatingXmlCreationSink(summarySink, resultsXmlAssembly);

            testSink.Execution.TestPassedEvent += args => { Console.WriteLine($"[PASS] {args.Message.Test.DisplayName}"); };
            testSink.Execution.TestSkippedEvent += args => { Console.WriteLine($"[SKIP] {args.Message.Test.DisplayName}"); };
            testSink.Execution.TestFailedEvent += (MessageHandlerArgs<ITestFailed> args) => HandleTestFailed(args);

            testSink.Execution.TestAssemblyStartingEvent += args => { Console.WriteLine($"Running tests for {args.Message.TestAssembly.Assembly}"); };
            testSink.Execution.TestAssemblyFinishedEvent += args => { Console.WriteLine($"Finished {args.Message.TestAssembly.Assembly}{Environment.NewLine}"); };

            controller.RunTests(testCasesToRun, resultsSink, testOptions);
            var threadpoolPump = typeof(ThreadPool).GetMethod("PumpThreadPool", BindingFlags.NonPublic | BindingFlags.Static);
            var timerPump = Type.GetType("System.Threading.TimerQueue")?.GetMethod("PumpTimerQueue", BindingFlags.NonPublic | BindingFlags.Static);

            if (threadpoolPump != null && timerPump != null)
            {
                while (!resultsSink.Finished.WaitOne(0))
                {
                    threadpoolPump.Invoke(this, null);
                    timerPump.Invoke(this, null);
                }
            }
            else
            {
                resultsSink.Finished.WaitOne();
            }

            if (printXml)
            {
                FixupFailedTestResultsForPNSE(resultsXmlAssembly);
                Console.WriteLine($"STARTRESULTXML");
                var resultsXml = new XElement("assemblies");
                resultsXml.Add(resultsXmlAssembly);
                resultsXml.Save(Console.OpenStandardOutput());
                Console.WriteLine();
                Console.WriteLine($"ENDRESULTXML");
            }

            var failed = resultsSink.ExecutionSummary.Failed > 0 || resultsSink.ExecutionSummary.Errors > 0;
            return failed ? 1 : 0;
        }

        void FixupFailedTestResultsForPNSE(XElement resultsXmlAssembly)
        {
            var pnse_tests = resultsXmlAssembly.Elements("collection").Descendants("test")
                .Where(test => test.Attributes("result").Any(r => string.Compare((string)r, "fail", StringComparison.OrdinalIgnoreCase) == 0))
                .Where(test => test.Descendants("failure").Any(f => IsTestFailurePNSE(f)));

            foreach (var test in pnse_tests)
                test.SetAttributeValue("result", "PNSE");

            bool IsTestFailurePNSE(XElement failure)
            {
                return failure.Attributes("exception-type")
                            .Any(attr => string.Compare((string)attr, "System.PlatformNotSupportedException", StringComparison.OrdinalIgnoreCase) == 0);
                        //|| failure.Descendants("message").Any(msg => ((string)msg).Contains("System.PlatformNotSupportedException"));
            }
        }

        void HandleTestFailed(MessageHandlerArgs<ITestFailed> args)
        {
            bool isPNSE = IsPNSE(args.Message);
            Console.WriteLine($"[{(isPNSE ? "PNSE" : "FAIL")}] {args.Message.Test.DisplayName}{Environment.NewLine}{ExceptionUtility.CombineMessages(args.Message)}{Environment.NewLine}{ExceptionUtility.CombineStackTraces(args.Message)}");
        }

        public static bool IsPNSE(ITestFailed failedTestMessage)
            => failedTestMessage.ExceptionTypes.Any(type => type == "System.PlatformNotSupportedException");
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

    internal class WasmExecutionSummarySink : LongLivedMarshalByRefObject, IExecutionSink
    {
        bool _disposed;
        IExecutionSink _innerSink;
        Action<string, WasmExecutionSummary>? _completionCallback;
        int _pnse;
        WasmExecutionSummary _wasmExecutionSummary = new WasmExecutionSummary();

        public WasmExecutionSummarySink(
            IExecutionSink innerSink,
            Func<bool>? cancelThunk = null,
            Action<string, WasmExecutionSummary>? completionCallback = null)
        {
            this._innerSink = innerSink;
            this._completionCallback = completionCallback;
        }

        public ExecutionSummary ExecutionSummary => _wasmExecutionSummary;

        public ManualResetEvent Finished => _innerSink.Finished;

        public void Dispose()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);

            _disposed = true;

            Finished.Dispose();
        }

        public bool OnMessageWithTypes(
            IMessageSinkMessage message,
            HashSet<string>? messageTypes)
        {
            return message.Dispatch<ITestFailed>(messageTypes, HandleTestFailed)
                    && message.Dispatch<ITestAssemblyFinished>(messageTypes, HandleTestAssemblyFinished)
                    && _innerSink.OnMessageWithTypes(message, messageTypes);
        }

        private void HandleTestFailed(MessageHandlerArgs<ITestFailed> args)
        {
            if (ThreadlessXunitTestRunner.IsPNSE(args.Message))
                _pnse++;
        }

        private void HandleTestAssemblyFinished(MessageHandlerArgs<ITestAssemblyFinished> args)
        {
            _wasmExecutionSummary.PNSE = _pnse;
            _wasmExecutionSummary.Total = args.Message.TestsRun;
            _wasmExecutionSummary.Failed = args.Message.TestsFailed - _pnse;
            _wasmExecutionSummary.Skipped = args.Message.TestsSkipped;
            _wasmExecutionSummary.Time = args.Message.ExecutionTime;

            _completionCallback?.Invoke(Path.GetFileNameWithoutExtension(args.Message.TestAssembly.Assembly.AssemblyPath), (ExecutionSummary as WasmExecutionSummary)!);

            Finished.Set();
        }
    }

    internal class WasmExecutionSummary : ExecutionSummary
    {
        public int PNSE { get; set; }
    }
}
