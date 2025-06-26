// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.TestRunners.Common;
using Xunit.v3;

#nullable enable
namespace Microsoft.DotNet.XHarness.TestRunners.Xunit.v3;

internal class XunitV3TestRunner : XunitV3TestRunnerBase
{
    private XElement _assembliesElement;

    protected override string ResultsFileName { get; set; } = "TestResults.xUnit.v3.xml";

    public XunitV3TestRunner(LogWriter logger) : base(logger)
    {
    }

    internal XElement ConsumeAssembliesElement()
    {
        Debug.Assert(_assembliesElement != null, "ConsumeAssembliesElement called before Run() or after ConsumeAssembliesElement() was already called.");
        var res = _assembliesElement;
        _assembliesElement = null;
        FailureInfos.Clear();
        return res;
    }

    public override async Task Run(IEnumerable<TestAssemblyInfo> testAssemblies)
    {
        _assembliesElement = new XElement("assemblies");

        foreach (var assemblyInfo in testAssemblies)
        {
            OnInfo($"Running tests in {assemblyInfo.AssemblyPath}...");
            
            // For now, create a simple placeholder implementation
            var assemblyElement = new XElement("assembly",
                new XAttribute("name", assemblyInfo.AssemblyPath),
                new XAttribute("total", 0),
                new XAttribute("passed", 0),
                new XAttribute("failed", 0),
                new XAttribute("skipped", 0),
                new XAttribute("time", "0.000"),
                new XAttribute("environment", "v3-placeholder"),
                new XAttribute("test-framework", "xUnit.net v3"));

            _assembliesElement.Add(assemblyElement);
            
            OnInfo($"Placeholder implementation for xUnit v3 - assembly {assemblyInfo.AssemblyPath} processed");
        }

        OnInfo("xUnit v3 test run completed (placeholder implementation)");
    }

    public override Task<string> WriteResultsToFile(XmlResultJargon jargon)
    {
        var path = Path.Combine(ResultsDirectory, ResultsFileName);
        Directory.CreateDirectory(ResultsDirectory);

        using var writer = new StreamWriter(path);
        return WriteResultsToFile(writer, jargon).ContinueWith(_ => path);
    }

    public override Task WriteResultsToFile(TextWriter writer, XmlResultJargon jargon)
    {
        if (_assembliesElement != null)
        {
            switch (jargon)
            {
                case XmlResultJargon.NUnitV2:
                case XmlResultJargon.NUnitV3:
                    OnInfo("XSLT transformation for NUnit format not yet implemented for xUnit v3");
                    goto default;
                default: // xunit as default
                    _assembliesElement.Save(writer);
                    break;
            }
        }
        return Task.CompletedTask;
    }
}