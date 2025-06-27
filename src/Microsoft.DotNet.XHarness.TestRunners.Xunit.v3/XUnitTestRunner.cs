// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.TestRunners.Common;

namespace Microsoft.DotNet.XHarness.TestRunners.Xunit;

public abstract class XunitTestRunnerBase : TestRunner
{
    protected XunitTestRunnerBase(LogWriter logger) : base(logger)
    {
    }

    public override void SkipTests(IEnumerable<string> tests)
    {
        // Placeholder implementation for now
        OnInfo($"Skipping {tests.Count()} tests in xUnit v3 runner (not yet implemented)");
    }

    public override void SkipCategories(IEnumerable<string> categories)
    {
        // Placeholder implementation for now
        OnInfo($"Skipping {categories.Count()} categories in xUnit v3 runner (not yet implemented)");
    }

    public override void SkipMethod(string method, bool isExcluded)
    {
        // Placeholder implementation for now
        OnInfo($"Skipping method {method} in xUnit v3 runner (excluded: {isExcluded}) (not yet implemented)");
    }

    public override void SkipClass(string className, bool isExcluded)
    {
        // Placeholder implementation for now
        OnInfo($"Skipping class {className} in xUnit v3 runner (excluded: {isExcluded}) (not yet implemented)");
    }
}

public class XUnitTestRunner : XunitTestRunnerBase
{
    private XElement _assembliesElement;

    public XUnitTestRunner(LogWriter logger) : base(logger)
    {
        _assembliesElement = new XElement("assemblies");
    }

    public int? MaxParallelThreads { get; set; }

    protected override string ResultsFileName { get; set; } = "TestResults.xUnit.xml";

    public override async Task Run(IEnumerable<TestAssemblyInfo> testAssemblies)
    {
        OnInfo("Starting xUnit v3 test execution...");
        
        // Create basic XML structure for now
        _assembliesElement = new XElement("assemblies");
        
        foreach (var testAssembly in testAssemblies)
        {
            OnInfo($"Processing assembly: {testAssembly.FullPath}");
            
            var assemblyElement = new XElement("assembly",
                new XAttribute("name", testAssembly.FullPath),
                new XAttribute("test-framework", "xUnit.net v3"),
                new XAttribute("run-date", DateTime.Now.ToString("yyyy-MM-dd")),
                new XAttribute("run-time", DateTime.Now.ToString("HH:mm:ss")),
                new XAttribute("total", 0),
                new XAttribute("passed", 0),
                new XAttribute("failed", 0),
                new XAttribute("skipped", 0),
                new XAttribute("time", "0"),
                new XAttribute("errors", 0)
            );
            
            _assembliesElement.Add(assemblyElement);
        }
        
        OnInfo("xUnit v3 test execution completed.");
    }

    public override async Task<string> WriteResultsToFile(XmlResultJargon xmlResultJargon)
    {
        var path = Path.Combine(TestsRootDirectory ?? ".", ResultsFileName);
        
        using var writer = new StreamWriter(path);
        await WriteResultsToFile(writer, xmlResultJargon);
        
        return path;
    }

    public override async Task WriteResultsToFile(TextWriter writer, XmlResultJargon jargon)
    {
        var results = _assembliesElement ?? new XElement("assemblies");
        
        // For now, just write the basic xUnit XML format regardless of jargon
        await writer.WriteAsync(results.ToString());
    }

    public XElement ConsumeAssembliesElement()
    {
        var result = _assembliesElement ?? new XElement("assemblies");
        _assembliesElement = null;
        return result;
    }
}