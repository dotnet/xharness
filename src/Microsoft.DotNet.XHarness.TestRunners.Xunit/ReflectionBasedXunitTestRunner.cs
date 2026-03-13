// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.TestRunners.Common;
using Xunit;

#nullable enable
namespace Microsoft.DotNet.XHarness.TestRunners.Xunit;

/// <summary>
/// Xunit test runner using reflection-based discovery (NativeAOT-safe)
/// with parallel test execution support and file-based result output.
/// </summary>
internal class ReflectionBasedXunitTestRunner : CustomXunitTestRunner
{
    public int? MaxParallelThreads { get; set; }

    public ReflectionBasedXunitTestRunner(LogWriter logger) : base(logger)
    {
    }

    protected override string RunnerDisplayName => "reflection-based Xunit runner (threaded execution)";

    private string _resultsFileName = "TestResults.xUnit.xml";
    protected override string ResultsFileName { get => _resultsFileName; set => _resultsFileName = value; }

    protected override TestAssemblyConfiguration CreateConfiguration()
    {
        int maxThreads = MaxParallelThreads ?? Environment.ProcessorCount;
        return new TestAssemblyConfiguration()
        {
            ShadowCopy = false,
            ParallelizeAssembly = false,
            ParallelizeTestCollections = RunInParallel,
            MaxParallelThreads = RunInParallel ? maxThreads : 1,
            PreEnumerateTheories = false,
        };
    }

    public override Task<string> WriteResultsToFile(XmlResultJargon xmlResultJargon)
    {
        if (_assembliesElement is null)
            return Task.FromResult(string.Empty);

        string outputFilePath = GetResultsFilePath();
        var settings = new XmlWriterSettings { Indent = true };
        using (var xmlWriter = XmlWriter.Create(outputFilePath, settings))
        {
            _assembliesElement.Save(xmlWriter);
        }

        return Task.FromResult(outputFilePath);
    }

    public override Task WriteResultsToFile(TextWriter writer, XmlResultJargon jargon)
    {
        if (_assembliesElement is null)
            return Task.CompletedTask;

        var settings = new XmlWriterSettings { Indent = true };
        using (var xmlWriter = XmlWriter.Create(writer, settings))
        {
            _assembliesElement.Save(xmlWriter);
        }

        return Task.CompletedTask;
    }
}
