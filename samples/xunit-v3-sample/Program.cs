// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Reflection;
using Microsoft.DotNet.XHarness.TestRunners.Common;
using Microsoft.DotNet.XHarness.TestRunners.Xunit.v3;

namespace XunitV3Sample;

// Sample test class using xunit v3
public class SampleTests
{
    [Fact]
    public void BasicTest()
    {
        Assert.True(true);
    }

    [Fact]
    public void AnotherTest()
    {
        Assert.Equal(4, 2 + 2);
    }
}

// Entry point demonstrating xunit v3 runner usage
public class Program : WasmApplicationEntryPoint
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("xunit v3 Sample Application");
        
        using var writer = new StringWriter();
        var logger = new LogWriter(writer);
        
        var runner = new XunitV3TestRunner(logger);
        
        // Run tests in this assembly
        var assemblyInfo = new TestAssemblyInfo(
            Assembly: Assembly.GetExecutingAssembly(),
            AssemblyPath: Assembly.GetExecutingAssembly().Location
        );
        
        await runner.Run(new[] { assemblyInfo });
        
        // Output results
        Console.WriteLine("Test Results:");
        var results = runner.ConsumeAssembliesElement();
        Console.WriteLine(results.ToString());
        
        Console.WriteLine("\nLogger Output:");
        Console.WriteLine(writer.ToString());
    }
}