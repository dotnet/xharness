// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

#nullable enable
namespace Microsoft.DotNet.XHarness.TestRunners.Common;

/// <summary>
/// Manages code coverage collection for XHarness device tests.
///
/// Looks for a coverage file produced by an external tool (e.g. coverlet) at the
/// configured output path or in the same directory. The file is then transported
/// back to the host by the platform-specific plumbing (adb pull, app container, etc.).
/// </summary>
public class CoverageManager
{
    public string OutputPath { get; }

    public CoverageManager(string? outputPath)
    {
        if (string.IsNullOrEmpty(outputPath))
        {
            // Default: same directory as iOS test results (Documents on iOS, temp elsewhere)
            var personalDir = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            OutputPath = !string.IsNullOrEmpty(personalDir)
                ? Path.Combine(personalDir, "coverage.cobertura.xml")
                : Path.Combine(Path.GetTempPath(), "coverage.cobertura.xml");
        }
        else if (!Path.IsPathRooted(outputPath))
        {
            // Resolve relative paths against the Personal/Documents directory on mobile
            var personalDir = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            OutputPath = !string.IsNullOrEmpty(personalDir)
                ? Path.Combine(personalDir, outputPath)
                : Path.Combine(Environment.CurrentDirectory, outputPath);
        }
        else
        {
            OutputPath = outputPath;
        }
    }

    /// <summary>
    /// Prepares for coverage collection. Call before test execution.
    /// Creates the output directory and sets the COVERAGE_OUTPUT_PATH environment variable
    /// so that on-device coverage tools know where to write their results.
    /// </summary>
    public void PrepareForCoverage()
    {
        var outputDir = Path.GetDirectoryName(OutputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        Environment.SetEnvironmentVariable("COVERAGE_OUTPUT_PATH", OutputPath);
    }

    /// <summary>
    /// Gets coverage results. Returns the path to the coverage file if an external tool
    /// has already written one, or null if no file was found.
    /// </summary>
    public string? GetCoverageResults()
    {
        if (File.Exists(OutputPath))
        {
            return OutputPath;
        }

        var directory = Path.GetDirectoryName(OutputPath);
        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
        {
            foreach (var file in Directory.GetFiles(directory, "coverage.cobertura.xml"))
            {
                return file;
            }
        }

        return null;
    }
}
