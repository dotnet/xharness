// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Linq;
using Microsoft.DotNet.XHarness.TestRunners.Common;
using Microsoft.DotNet.XHarness.TestRunners.Xunit.v3;
using Xunit.v3;

namespace Microsoft.DotNet.XHarness.TestRunners.Xunit.v3.Tests;

public class XunitV3TestRunnerTests
{
    [Fact]
    public void TestRunner_CanBeCreated()
    {
        using var writer = new StringWriter();
        var logger = new LogWriter(writer);
        var runner = new XunitV3TestRunner(logger);
        
        Assert.NotNull(runner);
        Assert.Equal("TestResults.xUnit.v3.xml", runner.ResultsFileName);
    }

    [Fact]
    public void TestRunner_UsesCorrectResultsFileName()
    {
        using var writer = new StringWriter();
        var logger = new LogWriter(writer);
        var runner = new XunitV3TestRunner(logger);
        
        Assert.Contains("v3", runner.ResultsFileName);
    }

    [Fact]
    public async Task TestRunner_CanRunEmptyAssemblyList()
    {
        using var writer = new StringWriter();
        var logger = new LogWriter(writer);
        var runner = new XunitV3TestRunner(logger);
        
        await runner.Run(Enumerable.Empty<TestAssemblyInfo>());
        
        var element = runner.ConsumeAssembliesElement();
        Assert.NotNull(element);
        Assert.Equal("assemblies", element.Name.LocalName);
    }

    [Fact]
    public async Task TestRunner_CanGenerateBasicResults()
    {
        using var writer = new StringWriter();
        var logger = new LogWriter(writer);
        var runner = new XunitV3TestRunner(logger);
        
        var assemblyInfo = new TestAssemblyInfo(
            Assembly: typeof(XunitV3TestRunnerTests).Assembly,
            AssemblyPath: "test.dll"
        );
        
        await runner.Run(new[] { assemblyInfo });
        
        var element = runner.ConsumeAssembliesElement();
        Assert.NotNull(element);
        
        var assemblyElement = element.Elements("assembly").FirstOrDefault();
        Assert.NotNull(assemblyElement);
        Assert.Equal("test.dll", assemblyElement.Attribute("name")?.Value);
        Assert.Equal("xUnit.net v3", assemblyElement.Attribute("test-framework")?.Value);
    }
}