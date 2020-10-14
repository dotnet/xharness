// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Xml;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared.XmlResults
{
    public class XUnitResultParser : IXmlResultParser
    {
        public (string resultLine, bool failed) ParseXml(TextReader stream, TextWriter? writer)
        {
            long total, errors, failed, notRun, inconclusive, ignored, skipped, invalid;
            total = errors = failed = notRun = inconclusive = ignored = skipped = invalid = 0L;
            using (var reader = XmlReader.Create(stream))
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "assembly")
                    {
                        long.TryParse(reader["total"], out var assemblyCount);
                        total += assemblyCount;
                        long.TryParse(reader["errors"], out var assemblyErrors);
                        errors += assemblyErrors;
                        long.TryParse(reader["failed"], out var assemblyFailures);
                        failed += assemblyFailures;
                        long.TryParse(reader["skipped"], out var assemblySkipped);
                        skipped += assemblySkipped;
                    }
                    if (writer != null && reader.NodeType == XmlNodeType.Element && reader.Name == "collection")
                    {
                        var testCaseName = reader["name"].Replace("Test collection for ", "");
                        writer.WriteLine(testCaseName);
                        var time = reader.GetAttribute("time") ?? "0"; // some nodes might not have the time :/
                                                                       // get the first node and then move in the siblings of the same type
                        reader.ReadToDescendant("test");
                        do
                        {
                            if (reader.Name != "test")
                            {
                                break;
                            }
                            // read the test cases in the current node
                            var status = reader["result"];
                            switch (status)
                            {
                                case "Pass":
                                    writer.Write("\t[PASS] ");
                                    break;
                                case "Skip":
                                    writer.Write("\t[IGNORED] ");
                                    break;
                                case "Fail":
                                    writer.Write("\t[FAIL] ");
                                    break;
                                default:
                                    writer.Write("\t[FAIL] ");
                                    break;
                            }
                            writer.Write(reader["name"]);
                            if (status == "Fail")
                            { //  we need to print the message
                                reader.ReadToDescendant("message");
                                writer.Write($" : {reader.ReadElementContentAsString()}");
                                reader.ReadToNextSibling("stack-trace");
                                writer.Write($" : {reader.ReadElementContentAsString()}");
                            }
                            // add a new line
                            writer.WriteLine();
                        } while (reader.ReadToNextSibling("test"));
                        writer.WriteLine($"{testCaseName} {time} ms");
                    }
                }
            }
            var passed = total - errors - failed - notRun - inconclusive - ignored - skipped - invalid;
            var resultLine = $"Tests run: {total} Passed: {passed} Inconclusive: {inconclusive} Failed: {failed + errors} Ignored: {ignored + skipped + invalid}";
            writer?.WriteLine(resultLine);

            return (resultLine, errors != 0 || failed != 0);
        }
    }
}
