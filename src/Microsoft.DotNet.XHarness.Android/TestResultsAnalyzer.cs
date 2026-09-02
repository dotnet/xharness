// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.DotNet.XHarness.Android;

/// <summary>
/// Reads test result files (XML) produced by test applications and tells whether they contain failed tests.
/// This is needed because some applications report a zero instrumentation exit code even when tests failed.
/// </summary>
public static class TestResultsAnalyzer
{
    /// <summary>
    /// Returns the number of failed tests found in given test results file.
    /// Returns null when the file is missing or its format is not recognized (in which case we can't tell).
    /// </summary>
    public static int? GetFailedTestCount(string resultsFilePath)
    {
        if (string.IsNullOrEmpty(resultsFilePath) || !File.Exists(resultsFilePath))
        {
            return null;
        }

        XDocument document;
        try
        {
            document = XDocument.Load(resultsFilePath);
        }
        catch (Exception)
        {
            return null;
        }

        XElement? root = document.Root;
        if (root == null)
        {
            return null;
        }

        switch (root.Name.LocalName)
        {
            // xUnit v2 format (the default of the XHarness test runners)
            case "assemblies":
            case "assembly":
                var assemblies = root.Name.LocalName == "assembly"
                    ? new[] { root }
                    : root.Elements().Where(e => e.Name.LocalName == "assembly").ToArray();

                // An empty <assemblies /> element is a valid result file with no failures
                return assemblies.Sum(assembly => GetIntAttribute(assembly, "failed") + GetIntAttribute(assembly, "errors"));

            // NUnit v2 format
            case "test-results":
                return GetIntAttribute(root, "failures") + GetIntAttribute(root, "errors");

            // NUnit v3 format
            case "test-run":
                return GetIntAttribute(root, "failed");

            default:
                return null;
        }
    }

    private static int GetIntAttribute(XElement element, string attributeName)
        => int.TryParse(element.Attribute(attributeName)?.Value, out int value) ? value : 0;
}
