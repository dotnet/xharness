// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.CLI.Resources;

namespace Microsoft.DotNet.XHarness.CLI;

/// <summary>
/// Example class demonstrating how to use localized strings in XHarness CLI
/// This class shows the localization infrastructure setup for the CLI project.
/// </summary>
internal static class LocalizationExample
{
    /// <summary>
    /// Example method showing how to access localized strings
    /// </summary>
    /// <returns>A localized example message</returns>
    public static string GetExampleLocalizedMessage()
    {
        // This demonstrates how to access localized strings using the resource infrastructure
        // The string can be translated by adding additional .resx files (e.g., Strings.es.resx for Spanish)
        return Strings.ExampleMessage;
    }
}