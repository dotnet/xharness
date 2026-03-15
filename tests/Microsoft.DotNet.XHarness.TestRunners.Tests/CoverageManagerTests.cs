// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.DotNet.XHarness.TestRunners.Common;
using Xunit;

#nullable enable
namespace Microsoft.DotNet.XHarness.TestRunners.Tests;

public class CoverageManagerTests : IDisposable
{
    private readonly string _tempDir;
    private static readonly TestAssemblyInfo[] s_emptyAssemblies = Array.Empty<TestAssemblyInfo>();

    public CoverageManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "xharness-coverage-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Constructor_UsesProvidedPath()
    {
        var path = Path.Combine(_tempDir, "my-coverage.xml");
        var manager = new CoverageManager(path);
        Assert.Equal(path, manager.OutputPath);
    }

    [Fact]
    public void Constructor_DefaultsToTempPath_WhenNull()
    {
        var manager = new CoverageManager(null);
        Assert.Contains("coverage.cobertura.xml", manager.OutputPath);
    }

    [Fact]
    public void PrepareForCoverage_CreatesOutputDirectory()
    {
        var subDir = Path.Combine(_tempDir, "nested", "output");
        var outputPath = Path.Combine(subDir, "coverage.xml");
        var manager = new CoverageManager(outputPath);

        manager.PrepareForCoverage(s_emptyAssemblies);

        Assert.True(Directory.Exists(subDir));
    }

    [Fact]
    public void PrepareForCoverage_SetsEnvironmentVariable()
    {
        var outputPath = Path.Combine(_tempDir, "coverage.xml");
        var manager = new CoverageManager(outputPath);

        manager.PrepareForCoverage(s_emptyAssemblies);

        Assert.Equal(outputPath, Environment.GetEnvironmentVariable("COVERAGE_OUTPUT_PATH"));
    }

    [Fact]
    public void GetCoverageResults_ReturnsPath_WhenFileExists()
    {
        var outputPath = Path.Combine(_tempDir, "coverage.cobertura.xml");
        File.WriteAllText(outputPath, "<coverage/>");

        var manager = new CoverageManager(outputPath);
        var result = manager.GetCoverageResults();

        Assert.Equal(outputPath, result);
    }

    [Fact]
    public void GetCoverageResults_FindsCoberturaFile_InSameDirectory()
    {
        File.WriteAllText(Path.Combine(_tempDir, "coverage.cobertura.xml"), "<coverage/>");
        var outputPath = Path.Combine(_tempDir, "other-name.xml");

        var manager = new CoverageManager(outputPath);
        var result = manager.GetCoverageResults();

        Assert.NotNull(result);
        Assert.EndsWith("coverage.cobertura.xml", result);
    }

    [Fact]
    public void GetCoverageResults_GeneratesReflectionCoverage_WhenNoFileExists()
    {
        var outputPath = Path.Combine(_tempDir, "coverage.cobertura.xml");
        var manager = new CoverageManager(outputPath);

        // Provide the current test assembly as a "test assembly"
        var asm = Assembly.GetExecutingAssembly();
        manager.PrepareForCoverage(new[] { new TestAssemblyInfo(asm, asm.Location) });

        var result = manager.GetCoverageResults();

        Assert.NotNull(result);
        Assert.True(File.Exists(result));
        var content = File.ReadAllText(result);
        Assert.Contains("<coverage", content);
        Assert.Contains("<package", content);
        Assert.Contains("line-rate", content);
    }

    [Fact]
    public void GetCoverageResults_ReturnsNull_WhenNoAssembliesAndNoFile()
    {
        var outputPath = Path.Combine(_tempDir, "nonexistent.xml");
        var manager = new CoverageManager(outputPath);
        // Don't call PrepareForCoverage (no assemblies)

        var result = manager.GetCoverageResults();

        Assert.Null(result);
    }
}
