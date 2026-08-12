// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.IO;
using Xunit;

namespace Microsoft.DotNet.XHarness.Android.Tests;

public class TestResultsAnalyzerTests : IDisposable
{
    private readonly string _tempDirectory;

    public TestResultsAnalyzerTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    [Fact]
    public void XUnitResultsWithFailuresAreDetected()
    {
        var path = WriteResultsFile(
            @"<assemblies>
                <assembly name=""System.Numerics.Vectors.Tests.dll"" total=""1194"" passed=""1191"" failed=""3"" skipped=""0"" errors=""0"" />
              </assemblies>");

        Assert.Equal(3, TestResultsAnalyzer.GetFailedTestCount(path));
    }

    [Fact]
    public void XUnitResultsWithoutFailuresAreDetected()
    {
        var path = WriteResultsFile(
            @"<assemblies>
                <assembly name=""System.Buffers.Tests.dll"" total=""100"" passed=""100"" failed=""0"" skipped=""0"" errors=""0"" />
              </assemblies>");

        Assert.Equal(0, TestResultsAnalyzer.GetFailedTestCount(path));
    }

    [Fact]
    public void XUnitErrorsAreCountedAsFailures()
    {
        var path = WriteResultsFile(
            @"<assembly name=""Some.Tests.dll"" total=""10"" passed=""9"" failed=""0"" skipped=""0"" errors=""1"" />");

        Assert.Equal(1, TestResultsAnalyzer.GetFailedTestCount(path));
    }

    [Fact]
    public void NUnitV2ResultsWithFailuresAreDetected()
    {
        var path = WriteResultsFile(
            @"<test-results name=""Some.Tests"" total=""10"" errors=""1"" failures=""2"" not-run=""0"" />");

        Assert.Equal(3, TestResultsAnalyzer.GetFailedTestCount(path));
    }

    [Fact]
    public void NUnitV3ResultsWithFailuresAreDetected()
    {
        var path = WriteResultsFile(
            @"<test-run id=""2"" testcasecount=""10"" result=""Failed"" total=""10"" passed=""8"" failed=""2"" />");

        Assert.Equal(2, TestResultsAnalyzer.GetFailedTestCount(path));
    }

    [Fact]
    public void EmptyXUnitResultsMeanNoFailures()
    {
        var path = WriteResultsFile(@"<assemblies />");

        Assert.Equal(0, TestResultsAnalyzer.GetFailedTestCount(path));
    }

    [Fact]
    public void UnknownFormatIsNotEvaluated()
    {
        var path = WriteResultsFile(@"<some-other-format failed=""3"" />");

        Assert.Null(TestResultsAnalyzer.GetFailedTestCount(path));
    }

    [Fact]
    public void MalformedFileIsNotEvaluated()
    {
        var path = WriteResultsFile("this is not XML");

        Assert.Null(TestResultsAnalyzer.GetFailedTestCount(path));
    }

    [Fact]
    public void MissingFileIsNotEvaluated()
        => Assert.Null(TestResultsAnalyzer.GetFailedTestCount(Path.Combine(_tempDirectory, "does-not-exist.xml")));

    private string WriteResultsFile(string content)
    {
        var path = Path.Combine(_tempDirectory, "testResults.xml");
        File.WriteAllText(path, content);
        return path;
    }
}
