// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Xml;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared.XmlResults;

public class TouchUnitTestReportGenerator : TestReportGenerator
{
    public override void GenerateFailure(XmlWriter writer, string title, string message, TextReader stderr)
    {
        // No-op - there was no implementation when we were splitting the parser up
    }

    public override void GenerateTestReport(TextWriter writer, XmlReader reader)
    {
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (reader.Name == "test-run")
            {
                var innerReader = new NUnitV3TestReportGenerator();
                innerReader.GenerateTestReport(writer, reader);
                return;
            }

            if (reader.Name == "test-results")
            {
                var innerReader = new NUnitV2TestReportGenerator();
                innerReader.GenerateTestReport(writer, reader);
                return;
            }
        }
    }
}
