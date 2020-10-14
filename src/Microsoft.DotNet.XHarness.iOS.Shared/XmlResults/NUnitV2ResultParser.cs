// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Xml;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared.XmlResults
{
    public class NUnitV2ResultParser : IXmlResultParser
    {
        public (string resultLine, bool failed) ParseXml(TextReader stream, TextWriter? writer)
        {
            long total, errors, failed, notRun, inconclusive, ignored, skipped, invalid;
            total = errors = failed = notRun = inconclusive = ignored = skipped = invalid = 0L;
            var settings = new XmlReaderSettings
            {
                ValidationType = ValidationType.None
            };

            using (var reader = XmlReader.Create(stream, settings))
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "test-results")
                    {
                        long.TryParse(reader["total"], out total);
                        long.TryParse(reader["errors"], out errors);
                        long.TryParse(reader["failures"], out failed);
                        long.TryParse(reader["not-run"], out notRun);
                        long.TryParse(reader["inconclusive"], out inconclusive);
                        long.TryParse(reader["ignored"], out ignored);
                        long.TryParse(reader["skipped"], out skipped);
                        long.TryParse(reader["invalid"], out invalid);
                    }
                    if (writer != null && reader.NodeType == XmlNodeType.Element && reader.Name == "test-suite" && (reader["type"] == "TestFixture" || reader["type"] == "TestCollection"))
                    {
                        var testCaseName = reader["name"];
                        writer.WriteLine(testCaseName);
                        var time = reader.GetAttribute("time") ?? "0"; // some nodes might not have the time :/
                                                                       // get the first node and then move in the siblings of the same type
                        reader.ReadToDescendant("test-case");
                        do
                        {
                            if (reader.Name != "test-case")
                            {
                                break;
                            }

                            // read the test cases in the current node
                            var status = reader["result"];
                            switch (status)
                            {
                                case "Success":
                                    writer.Write("\t[PASS] ");
                                    break;
                                case "Ignored":
                                    writer.Write("\t[IGNORED] ");
                                    break;
                                case "Error":
                                case "Failure":
                                    writer.Write("\t[FAIL] ");
                                    break;
                                case "Inconclusive":
                                    writer.Write("\t[INCONCLUSIVE] ");
                                    break;
                                default:
                                    writer.Write("\t[INFO] ");
                                    break;
                            }
                            writer.Write(reader["name"]);
                            if (status == "Failure" || status == "Error")
                            { //  we need to print the message
                                reader.ReadToDescendant("message");
                                writer.Write($" : {reader.ReadElementContentAsString()}");
                                reader.ReadToNextSibling("stack-trace");
                                writer.Write($" : {reader.ReadElementContentAsString()}");
                            }
                            // add a new line
                            writer.WriteLine();
                        } while (reader.ReadToNextSibling("test-case"));
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
