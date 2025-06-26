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
using Xunit.v3;

namespace Microsoft.DotNet.XHarness.TestRunners.Xunit.V3;

internal class XsltIdGenerator
{
    // NUnit3 xml does not have schema, there is no much info about it, most examples just have incremental IDs.
    private int _seed = 1000;
    public int GenerateHash() => _seed++;
}

internal class XUnitTestRunner : XunitTestRunnerBase
{
    // TODO: Adapt for xunit v3 API when available
    // This is a placeholder implementation that will need to be updated
    // once we understand the xunit v3 API better
    
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
        // TODO: Initialize xunit v3 message sink and event handlers
        // This will need to be adapted based on the actual xunit v3 API
    }

    public override async Task Run(IEnumerable<TestAssemblyInfo> testAssemblies)
    {
        // TODO: Implement xunit v3 test execution
        // This is a placeholder that needs to be completed with actual xunit v3 API calls
        
        OnInfo("XUnit v3 Test Runner starting...");
        
        // Create assemblies element for results
        _assembliesElement = new XElement("assemblies");
        
        foreach (var assemblyInfo in testAssemblies)
        {
            OnInfo($"Running tests in {assemblyInfo.AssemblyPath}");
            
            // TODO: Load and run tests using xunit v3 API
            // This would replace the xunit v2 specific code from the original implementation
            
            // Placeholder assembly element
            var assemblyElement = new XElement("assembly",
                new XAttribute("name", assemblyInfo.AssemblyPath),
                new XAttribute("run-date", DateTime.Now.ToString("yyyy-MM-dd")),
                new XAttribute("run-time", DateTime.Now.ToString("HH:mm:ss")),
                new XAttribute("total", 0),
                new XAttribute("passed", 0),
                new XAttribute("failed", 0),
                new XAttribute("skipped", 0),
                new XAttribute("time", "0"));
                
            _assembliesElement.Add(assemblyElement);
        }
        
        OnInfo("XUnit v3 Test Runner completed.");
    }

    public override Task<string> WriteResultsToFile(XmlResultJargon jargon)
    {
        var outputFilePath = Path.Combine(ResultsDirectory ?? Path.GetTempPath(), ResultsFileName);
        
        using var writer = new StreamWriter(outputFilePath);
        WriteResultsToFile(writer, jargon).Wait();
        
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
        var xmlTransform = new XslCompiledTransform();
        var xmlTransformSettings = new XsltSettings { EnableDocumentFunction = true, EnableScript = true };
        using var xsltStream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Microsoft.DotNet.XHarness.TestRunners.Xunit.V3.{xsltResourceName}");
        if (xsltStream == null)
        {
            throw new InvalidOperationException($"Could not find embedded resource {xsltResourceName}");
        }
        using var xsltReader = XmlReader.Create(xsltStream);
        xmlTransform.Load(xsltReader, xmlTransformSettings, new XmlUrlResolver());
        
        using var xmlReader = element.CreateReader();
        xmlTransform.Transform(xmlReader, writer);
    }
}