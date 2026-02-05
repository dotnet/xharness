// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.TestRunners.Common;
using Xunit;

namespace Microsoft.DotNet.XHarness.TestRunners.Xunit.v3.Tests;

public class XUnitTestRunnerTests
{
    [Fact]
    public void TestRunner_CanBeCreated()
    {
        using var writer = new StringWriter();
        var logger = new LogWriter(writer);
        var runner = new XUnitTestRunner(logger);
        
        Assert.NotNull(runner);
    }

    [Fact]
    public async Task TestRunner_CanRunEmptyAssemblyList()
    {
        using var writer = new StringWriter();
        var logger = new LogWriter(writer);
        var runner = new XUnitTestRunner(logger);
        
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
        var runner = new XUnitTestRunner(logger);
        
        var assemblyInfo = new TestAssemblyInfo(
            typeof(XUnitTestRunnerTests).Assembly,
            "test.dll"
        );
        
        await runner.Run(new[] { assemblyInfo });
        
        var element = runner.ConsumeAssembliesElement();
        Assert.NotNull(element);
        
        var assemblyElement = element.Elements("assembly").FirstOrDefault();
        Assert.NotNull(assemblyElement);
        Assert.Equal("test.dll", assemblyElement.Attribute("name")?.Value);
        Assert.Contains("xUnit.net", assemblyElement.Attribute("test-framework")?.Value);
    }
}