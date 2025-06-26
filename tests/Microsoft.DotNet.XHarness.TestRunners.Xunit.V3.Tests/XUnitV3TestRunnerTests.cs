// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.TestRunners.Xunit.V3;
using Xunit;

#nullable enable
namespace Microsoft.DotNet.XHarness.TestRunners.Tests.Xunit.V3;

public class XUnitV3TestRunnerTests
{
    [Fact]
    public void TestRunner_CanBeCreated()
    {
        var logWriter = new MemoryLogWriter();
        var testRunner = new XUnitTestRunner(logWriter);
        
        Assert.NotNull(testRunner);
        Assert.Equal("TestResults.xUnit.xml", testRunner.ResultsFileName);
    }

    [Fact]
    public void TestRunner_SupportsXmlResultJargons()
    {
        var logWriter = new MemoryLogWriter();
        var testRunner = new XUnitTestRunner(logWriter);
        
        // Test that the runner can handle different XML result formats
        var writer = new System.IO.StringWriter();
        var task = testRunner.WriteResultsToFile(writer, XmlResultJargon.xUnit);
        
        Assert.NotNull(task);
    }
}