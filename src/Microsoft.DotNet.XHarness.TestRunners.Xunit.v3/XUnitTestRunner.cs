// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.TestRunners.Common;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;
using Xunit.Runner.Common;
using TestAssemblyInfo = Microsoft.DotNet.XHarness.TestRunners.Common.TestAssemblyInfo;

namespace Microsoft.DotNet.XHarness.TestRunners.Xunit;

internal class XsltIdGenerator
{
    // NUnit3 xml does not have schema, there is no much info about it, most examples just have incremental IDs.
    private int _seed = 1000;
    public int GenerateHash() => _seed++;
}

public abstract class XunitTestRunnerBase : TestRunner
{
    private protected XUnitFiltersCollection _filters = new();

    protected XunitTestRunnerBase(LogWriter logger) : base(logger)
    {
    }

    public override void SkipTests(IEnumerable<string> tests)
    {
        if (tests.Any())
        {
            // create a single filter per test
            foreach (var t in tests)
            {
                if (t.StartsWith("KLASS:", StringComparison.Ordinal))
                {
                    var klass = t.Replace("KLASS:", "");
                    _filters.Add(XUnitFilter.CreateClassFilter(klass, true));
                }
                else if (t.StartsWith("KLASS32:", StringComparison.Ordinal) && IntPtr.Size == 4)
                {
                    var klass = t.Replace("KLASS32:", "");
                    _filters.Add(XUnitFilter.CreateClassFilter(klass, true));
                }
                else if (t.StartsWith("KLASS64:", StringComparison.Ordinal) && IntPtr.Size == 8)
                {
                    var klass = t.Replace("KLASS32:", "");
                    _filters.Add(XUnitFilter.CreateClassFilter(klass, true));
                }
                else if (t.StartsWith("Platform32:", StringComparison.Ordinal) && IntPtr.Size == 4)
                {
                    var filter = t.Replace("Platform32:", "");
                    _filters.Add(XUnitFilter.CreateSingleFilter(filter, true));
                }
                else
                {
                    _filters.Add(XUnitFilter.CreateSingleFilter(t, true));
                }
            }
        }
    }

    public override void SkipCategories(IEnumerable<string> categories) => SkipCategories(categories, isExcluded: true);

    public virtual void SkipCategories(IEnumerable<string> categories, bool isExcluded)
    {
        if (categories == null)
        {
            throw new ArgumentNullException(nameof(categories));
        }

        foreach (var c in categories)
        {
            var traitInfo = c.Split('=');
            if (traitInfo.Length == 2)
            {
                _filters.Add(XUnitFilter.CreateTraitFilter(traitInfo[0], traitInfo[1], isExcluded));
            }
            else
            {
                _filters.Add(XUnitFilter.CreateTraitFilter(c, null, isExcluded));
            }
        }
    }

    public override void SkipMethod(string method, bool isExcluded)
        => _filters.Add(XUnitFilter.CreateSingleFilter(singleTestName: method, exclude: isExcluded));

    public override void SkipClass(string className, bool isExcluded)
        => _filters.Add(XUnitFilter.CreateClassFilter(className: className, exclude: isExcluded));

    public virtual void SkipNamespace(string namespaceName, bool isExcluded)
        => _filters.Add(XUnitFilter.CreateNamespaceFilter(namespaceName, exclude: isExcluded));
}

public class XUnitTestRunner : XunitTestRunnerBase
{
    private readonly V3MessageSink _messageSink;

    public int? MaxParallelThreads { get; set; }

    private XElement _assembliesElement;

    internal XElement ConsumeAssembliesElement()
    {
        Debug.Assert(_assembliesElement != null, "ConsumeAssembliesElement called before Run() or after ConsumeAssembliesElement() was already called.");
        var res = _assembliesElement;
        _assembliesElement = null;
        FailureInfos.Clear();
        return res;
    }

    protected override string ResultsFileName { get; set; } = "TestResults.xUnit.xml";

    protected string TestStagePrefix { get; init; } = "\t";

    public XUnitTestRunner(LogWriter logger) : base(logger)
    {
        _messageSink = new V3MessageSink(this);
    }

    public void AddFilter(XUnitFilter filter)
    {
        if (filter != null)
        {
            _filters.Add(filter);
        }
    }

    public void SetFilters(List<XUnitFilter> newFilters)
    {
        if (newFilters == null)
        {
            _filters = null;
            return;
        }

        if (_filters == null)
        {
            _filters = new XUnitFiltersCollection();
        }

        _filters.AddRange(newFilters);
    }

    protected string GetThreadIdForLog()
    {
        if (EnvironmentVariables.IsLogThreadId())
            return $"[{Thread.CurrentThread.ManagedThreadId}]";

        return string.Empty;
    }

    private Action<string> EnsureLogger(Action<string> log) => log ?? OnInfo;

    private void do_log(string message, Action<string> log = null, StringBuilder sb = null)
    {
        log = EnsureLogger(log);

        if (sb != null)
        {
            sb.Append(message);
        }

        log(message);
    }

    public override async Task Run(IEnumerable<TestAssemblyInfo> testAssemblies)
    {
        if (testAssemblies == null)
        {
            throw new ArgumentNullException(nameof(testAssemblies));
        }

        if (_filters != null && _filters.Count > 0)
        {
            do_log("Configured filters:");
            foreach (XUnitFilter filter in _filters)
            {
                do_log($"  {filter}");
            }
        }

        _assembliesElement = new XElement("assemblies");
        Action<string> log = LogExcludedTests ? (s) => do_log(s) : (Action<string>)null;
        foreach (TestAssemblyInfo assemblyInfo in testAssemblies)
        {
            if (assemblyInfo == null || assemblyInfo.Assembly == null)
            {
                continue;
            }

            if (_filters.AssemblyFilters.Any() && _filters.IsExcluded(assemblyInfo, log))
            {
                continue;
            }

            if (string.IsNullOrEmpty(assemblyInfo.FullPath))
            {
                OnWarning($"Assembly '{assemblyInfo.Assembly}' cannot be found on the filesystem. xUnit requires access to actual on-disk file.");
                continue;
            }

            OnInfo($"Assembly: {assemblyInfo.Assembly} ({assemblyInfo.FullPath})");
            XElement assemblyElement = null;
            try
            {
                OnAssemblyStart(assemblyInfo.Assembly);
                assemblyElement = await Run(assemblyInfo.Assembly, assemblyInfo.FullPath).ConfigureAwait(false);
            }
            catch (FileNotFoundException ex)
            {
                OnWarning($"Assembly '{assemblyInfo.Assembly}' using path '{assemblyInfo.FullPath}' cannot be found on the filesystem. xUnit requires access to actual on-disk file.");
                OnWarning($"Exception is '{ex}'");
            }
            finally
            {
                OnAssemblyFinish(assemblyInfo.Assembly);
                if (assemblyElement != null)
                {
                    _assembliesElement.Add(assemblyElement);
                }
            }
        }

        LogFailureSummary();
        TotalTests += FilteredTests; // ensure that we do have in the total run the excluded ones.
    }

    public override Task<string> WriteResultsToFile(XmlResultJargon jargon)
    {
        if (_assembliesElement == null)
        {
            return Task.FromResult(string.Empty);
        }
        // remove all the empty nodes
        _assembliesElement.Descendants().Where(e => e.Name == "collection" && !e.Descendants().Any()).Remove();
        string outputFilePath = GetResultsFilePath();
        var settings = new XmlWriterSettings { Indent = true };
        using (var xmlWriter = XmlWriter.Create(outputFilePath, settings))
        {
            switch (jargon)
            {
                case XmlResultJargon.TouchUnit:
                case XmlResultJargon.NUnitV2:
                    Transform_Results("NUnitXml.xslt", _assembliesElement, xmlWriter);
                    break;
                case XmlResultJargon.NUnitV3:
                    Transform_Results("NUnit3Xml.xslt", _assembliesElement, xmlWriter);
                    break;
                default: // xunit as default, includes when we got Missing
                    _assembliesElement.Save(xmlWriter);
                    break;
            }
        }

        return Task.FromResult(outputFilePath);
    }

    public override Task WriteResultsToFile(TextWriter writer, XmlResultJargon jargon)
    {
        if (_assembliesElement == null)
        {
            return Task.CompletedTask;
        }
        // remove all the empty nodes
        _assembliesElement.Descendants().Where(e => e.Name == "collection" && !e.Descendants().Any()).Remove();
        var settings = new XmlWriterSettings { Indent = true };
        using (var xmlWriter = XmlWriter.Create(writer, settings))
        {
            switch (jargon)
            {
                case XmlResultJargon.TouchUnit:
                case XmlResultJargon.NUnitV2:
                    try
                    {
                        Transform_Results("NUnitXml.xslt", _assembliesElement, xmlWriter);
                    }
                    catch (Exception e)
                    {
                        writer.WriteLine(e);
                    }
                    break;
                case XmlResultJargon.NUnitV3:
                    try
                    {
                        Transform_Results("NUnit3Xml.xslt", _assembliesElement, xmlWriter);
                    }
                    catch (Exception e)
                    {
                        writer.WriteLine(e);
                    }
                    break;
                default: // xunit as default, includes when we got Missing
                    _assembliesElement.Save(xmlWriter);
                    break;
            }
        }
        return Task.CompletedTask;
    }

    private void Transform_Results(string xsltResourceName, XElement element, XmlWriter writer)
    {
        var xmlTransform = new System.Xml.Xsl.XslCompiledTransform();
        var name = GetType().Assembly.GetManifestResourceNames().Where(a => a.EndsWith(xsltResourceName, StringComparison.Ordinal)).FirstOrDefault();
        if (name == null)
        {
            return;
        }

        using (var xsltStream = GetType().Assembly.GetManifestResourceStream(name))
        {
            if (xsltStream == null)
            {
                throw new Exception($"Stream with name {name} cannot be found! We have {GetType().Assembly.GetManifestResourceNames()[0]}");
            }
            // add the extension so that we can get the hash from the name of the test
            // Create an XsltArgumentList.
            var xslArg = new XsltArgumentList();

            var generator = new XsltIdGenerator();
            xslArg.AddExtensionObject("urn:hash-generator", generator);

            using (var xsltReader = XmlReader.Create(xsltStream))
            using (var xmlReader = element.CreateReader())
            {
                xmlTransform.Load(xsltReader);
                xmlTransform.Transform(xmlReader, xslArg, writer);
            }
        }
    }

    protected virtual Stream GetConfigurationFileStream(Assembly assembly)
    {
        if (assembly == null)
        {
            throw new ArgumentNullException(nameof(assembly));
        }

        string path = assembly.Location?.Trim();
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        path = Path.Combine(path, ".xunit.runner.json");
        if (!File.Exists(path))
        {
            return null;
        }

        return File.OpenRead(path);
    }

    protected virtual TestAssemblyConfiguration GetConfiguration(Assembly assembly)
    {
        if (assembly == null)
        {
            throw new ArgumentNullException(nameof(assembly));
        }

        Stream configStream = GetConfigurationFileStream(assembly);
        if (configStream != null)
        {
            using (configStream)
            {
                return ConfigReader_Json.Load(configStream);
            }
        }

        return null;
    }

    protected virtual ITestFrameworkDiscoveryOptions GetFrameworkOptionsForDiscovery(TestAssemblyConfiguration configuration)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        return TestFrameworkOptions.ForDiscovery(configuration);
    }

    protected virtual ITestFrameworkExecutionOptions GetFrameworkOptionsForExecution(TestAssemblyConfiguration configuration)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        return TestFrameworkOptions.ForExecution(configuration);
    }

    private async Task<XElement> Run(Assembly assembly, string assemblyPath)
    {
        var testFramework = ExtensibilityPointFactory.GetTestFramework(assemblyPath);
        using (var frontController = new InProcessFrontController(testFramework, assembly, configFilePath: null))
        {
            var configuration = GetConfiguration(assembly) ?? new TestAssemblyConfiguration() { PreEnumerateTheories = false };
            ITestFrameworkDiscoveryOptions discoveryOptions = GetFrameworkOptionsForDiscovery(configuration);
            discoveryOptions.SetSynchronousMessageReporting(true);
            
            Logger.OnDebug($"Starting test discovery in the '{assembly}' assembly");

            var testCases = new List<ITestCaseDiscovered>();
            var discoverySink = new TestCaseDiscoverySink(testCases);

            await frontController.Find(
                discoverySink,
                discoveryOptions,
                filter: null,
                new CancellationTokenSource(),
                types: null,
                discoveryCallback: null
            ).ConfigureAwait(false);

            Logger.OnDebug($"Test discovery in assembly '{assembly}' completed");

            if (testCases.Count == 0)
            {
                Logger.Info("No test cases discovered");
                return null;
            }

            TotalTests += testCases.Count;
            List<ITestCaseDiscovered> filteredTestCases;
            if (_filters != null && _filters.TestCaseFilters.Any())
            {
                Action<string> log = LogExcludedTests ? (s) => do_log(s) : (Action<string>)null;
                filteredTestCases = testCases.Where(
                    tc => !_filters.IsExcluded(tc, log)).ToList();
                FilteredTests += testCases.Count - filteredTestCases.Count;
            }
            else
            {
                filteredTestCases = testCases;
            }

            var resultsXmlAssembly = new XElement("assembly");
            _messageSink.SetAssemblyElement(resultsXmlAssembly);

            ITestFrameworkExecutionOptions executionOptions = GetFrameworkOptionsForExecution(configuration);
            executionOptions.SetSynchronousMessageReporting(true);

            if (MaxParallelThreads.HasValue)
            {
                executionOptions.SetMaxParallelThreads(MaxParallelThreads.Value);
            }

            await frontController.Run(
                _messageSink,
                executionOptions,
                filteredTestCases.Select(tc => tc.Serialization).ToList(),
                new CancellationTokenSource()
            ).ConfigureAwait(false);

            return resultsXmlAssembly;
        }
    }

    // Inner class to handle xunit v3 messages
    private class V3MessageSink : IMessageSink
    {
        private readonly XUnitTestRunner _runner;
        private XElement _assemblyElement;
        private readonly Dictionary<string, XElement> _collectionElements = new();
        private readonly Dictionary<string, XElement> _testElements = new();

        public V3MessageSink(XUnitTestRunner runner)
        {
            _runner = runner;
        }

        public void SetAssemblyElement(XElement assemblyElement)
        {
            _assemblyElement = assemblyElement;
        }

        public bool OnMessage(IMessageSinkMessage message)
        {
            try
            {
                return message switch
                {
                    ITestAssemblyStarting tas => HandleTestAssemblyStarting(tas),
                    ITestAssemblyFinished taf => HandleTestAssemblyFinished(taf),
                    ITestCollectionStarting tcs => HandleTestCollectionStarting(tcs),
                    ITestCollectionFinished tcf => HandleTestCollectionFinished(tcf),
                    ITestStarting ts => HandleTestStarting(ts),
                    ITestPassed tp => HandleTestPassed(tp),
                    ITestFailed tf => HandleTestFailed(tf),
                    ITestSkipped tsk => HandleTestSkipped(tsk),
                    ITestFinished tfi => HandleTestFinished(tfi),
                    IDiagnosticMessage dm => HandleDiagnosticMessage(dm),
                    IErrorMessage em => HandleErrorMessage(em),
                    _ => true
                };
            }
            catch (Exception ex)
            {
                _runner.OnError($"Error handling message: {ex}");
                return true;
            }
        }

        private bool HandleTestAssemblyStarting(ITestAssemblyStarting message)
        {
            _runner.OnInfo($"[Test framework: {message.TestFrameworkDisplayName}]");
            if (_assemblyElement != null)
            {
                _assemblyElement.SetAttributeValue("name", message.TestAssemblyUniqueID);
                _assemblyElement.SetAttributeValue("test-framework", message.TestFrameworkDisplayName);
                _assemblyElement.SetAttributeValue("run-date", DateTime.Now.ToString("yyyy-MM-dd"));
                _assemblyElement.SetAttributeValue("run-time", DateTime.Now.ToString("HH:mm:ss"));
            }
            return true;
        }

        private bool HandleTestAssemblyFinished(ITestAssemblyFinished message)
        {
            _runner.TotalTests = message.TestsTotal;
            if (_assemblyElement != null)
            {
                _assemblyElement.SetAttributeValue("total", message.TestsTotal);
                _assemblyElement.SetAttributeValue("passed", message.TestsNotRun);
                _assemblyElement.SetAttributeValue("failed", message.TestsFailed);
                _assemblyElement.SetAttributeValue("skipped", message.TestsSkipped);
                _assemblyElement.SetAttributeValue("time", message.ExecutionTime.ToString("0.000"));
                _assemblyElement.SetAttributeValue("errors", 0);
            }
            return true;
        }

        private bool HandleTestCollectionStarting(ITestCollectionStarting message)
        {
            _runner.OnInfo($"\n{message.TestCollectionDisplayName}");
            if (_assemblyElement != null)
            {
                var collectionElement = new XElement("collection",
                    new XAttribute("name", message.TestCollectionDisplayName),
                    new XAttribute("total", 0),
                    new XAttribute("passed", 0),
                    new XAttribute("failed", 0),
                    new XAttribute("skipped", 0),
                    new XAttribute("time", "0")
                );
                _assemblyElement.Add(collectionElement);
                _collectionElements[message.TestCollectionUniqueID] = collectionElement;
            }
            return true;
        }

        private bool HandleTestCollectionFinished(ITestCollectionFinished message)
        {
            if (_collectionElements.TryGetValue(message.TestCollectionUniqueID, out var collectionElement))
            {
                collectionElement.SetAttributeValue("total", message.TestsTotal);
                collectionElement.SetAttributeValue("passed", message.TestsNotRun);
                collectionElement.SetAttributeValue("failed", message.TestsFailed);
                collectionElement.SetAttributeValue("skipped", message.TestsSkipped);
                collectionElement.SetAttributeValue("time", message.ExecutionTime.ToString("0.000"));
            }
            return true;
        }

        private bool HandleTestStarting(ITestStarting message)
        {
            if (EnvironmentVariables.IsLogTestStart())
            {
                _runner.OnInfo($"{_runner.TestStagePrefix}[STRT]{_runner.GetThreadIdForLog()} {message.TestDisplayName}");
            }
            _runner.OnTestStarted(message.TestDisplayName);
            return true;
        }

        private bool HandleTestPassed(ITestPassed message)
        {
            _runner.PassedTests++;
            _runner.OnInfo($"{_runner.TestStagePrefix}[PASS]{_runner.GetThreadIdForLog()} {message.TestDisplayName}");
            
            AddTestElement(message);
            
            _runner.OnTestCompleted((
                TestName: message.TestDisplayName,
                TestResult: TestResult.Passed
            ));
            return true;
        }

        private bool HandleTestFailed(ITestFailed message)
        {
            _runner.FailedTests++;
            var sb = new StringBuilder($"{_runner.TestStagePrefix}[FAIL]{_runner.GetThreadIdForLog()} {message.TestDisplayName}");
            sb.AppendLine();
            sb.AppendLine($"   {string.Join(Environment.NewLine, message.Messages)}");
            sb.AppendLine($"   {string.Join(Environment.NewLine, message.StackTraces)}");
            
            _runner.FailureInfos.Add(new TestFailureInfo
            {
                TestName = message.TestDisplayName,
                Message = sb.ToString()
            });
            
            _runner.OnError(sb.ToString());
            
            AddTestElement(message, failed: true);
            
            _runner.OnTestCompleted((
                TestName: message.TestDisplayName,
                TestResult: TestResult.Failed
            ));
            return true;
        }

        private bool HandleTestSkipped(ITestSkipped message)
        {
            _runner.SkippedTests++;
            _runner.OnInfo($"{_runner.TestStagePrefix}[IGNORED] {message.TestDisplayName}");
            
            AddTestElement(message, skipped: true);
            
            _runner.OnTestCompleted((
                TestName: message.TestDisplayName,
                TestResult: TestResult.Skipped
            ));
            return true;
        }

        private bool HandleTestFinished(ITestFinished message)
        {
            _runner.ExecutedTests++;
            return true;
        }

        private bool HandleDiagnosticMessage(IDiagnosticMessage message)
        {
            _runner.OnDiagnostic(message.Message);
            return true;
        }

        private bool HandleErrorMessage(IErrorMessage message)
        {
            _runner.OnError($"Error: {string.Join(Environment.NewLine, message.Messages)}");
            _runner.OnError($"{string.Join(Environment.NewLine, message.StackTraces)}");
            return true;
        }

        private void AddTestElement(ITestResultMessage message, bool failed = false, bool skipped = false)
        {
            if (!_collectionElements.TryGetValue(message.TestCollectionUniqueID, out var collectionElement))
            {
                return;
            }

            var testElement = new XElement("test",
                new XAttribute("name", message.TestDisplayName),
                new XAttribute("type", message.TestClassUniqueID ?? ""),
                new XAttribute("method", message.TestMethodUniqueID ?? ""),
                new XAttribute("time", message.ExecutionTime.ToString("0.000")),
                new XAttribute("result", failed ? "Fail" : (skipped ? "Skip" : "Pass"))
            );

            if (failed && message is ITestFailed failedMessage)
            {
                var failureElement = new XElement("failure",
                    new XAttribute("exception-type", failedMessage.ExceptionTypes.FirstOrDefault() ?? "Exception"),
                    new XElement("message", string.Join(Environment.NewLine, failedMessage.Messages)),
                    new XElement("stack-trace", string.Join(Environment.NewLine, failedMessage.StackTraces))
                );
                testElement.Add(failureElement);
            }

            if (skipped && message is ITestSkipped skippedMessage)
            {
                testElement.Add(new XElement("reason", skippedMessage.Reason));
            }

            if (!string.IsNullOrEmpty(message.Output))
            {
                testElement.Add(new XElement("output", message.Output));
            }

            collectionElement.Add(testElement);
        }
    }

    // Helper class to collect discovered test cases
    private class TestCaseDiscoverySink : IMessageSink
    {
        private readonly List<ITestCaseDiscovered> _testCases;

        public TestCaseDiscoverySink(List<ITestCaseDiscovered> testCases)
        {
            _testCases = testCases;
        }

        public bool OnMessage(IMessageSinkMessage message)
        {
            if (message is ITestCaseDiscovered testCase)
            {
                _testCases.Add(testCase);
            }
            return true;
        }
    }
}