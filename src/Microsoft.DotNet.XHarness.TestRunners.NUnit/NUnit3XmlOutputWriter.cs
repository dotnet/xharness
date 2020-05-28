// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Xml;
using Microsoft.DotNet.XHarness.Common;
using NUnit.Framework.Internal;

#nullable enable
namespace Microsoft.DotNet.XHarness.TestRunners.NUnit
{
    /// <summary>
    ///     NUnit3XmlOutputWriter is responsible for writing the results
    ///     of a test to a file in NUnit 3.0 format.
    /// </summary>
    internal class NUnit3XmlOutputWriter : OutputWriter
    {
        readonly DateTime _runStartTime;
        XmlWriter? _xmlWriter;

        public NUnit3XmlOutputWriter(DateTime runStartTime) => _runStartTime = runStartTime;

        /// <summary>
        ///     Writes the test result to the specified TextWriter
        /// </summary>
        /// <param name="result">The result to be written to a file</param>
        /// <param name="writer">A TextWriter to which the result is written</param>
        public override void WriteResultFile(IResultSummary result, TextWriter writer)
        {
            // NOTE: Under .NET 1.1, XmlTextWriter does not implement IDisposable,
            // but does implement Close(). Hence we cannot use a 'using' clause.
            var xmlWriter = new XmlTextWriter(writer);
            xmlWriter.Formatting = Formatting.Indented;

            try
            {
                WriteXmlOutput(result, xmlWriter);
            }
            finally
            {
                xmlWriter.Close();
            }
        }

        void WriteXmlOutput(IResultSummary result, XmlWriter xmlWriter)
        {
            _xmlWriter = xmlWriter;

            InitializeXmlFile(result);
            WriteResultElement(result);
            TerminateXmlFile();
        }

        void InitializeXmlFile(IResultSummary result)
        {
            if (_xmlWriter == null) // should never happen, would mean a programmers error
                throw new InvalidOperationException("Null writer");
            _xmlWriter.WriteStartDocument(false);

            // In order to match the format used by NUnit 3.0, we
            // wrap the entire result from the framework in a
            // <test-run> element.
            _xmlWriter.WriteStartElement("test-run");

            _xmlWriter.WriteAttributeString("id", "2"); // TODO: Should not be hard-coded
            _xmlWriter.WriteAttributeString("name", result.Name);
            _xmlWriter.WriteAttributeString("fullname", result.FullName);
            _xmlWriter.WriteAttributeString("testcasecount", result.TotalTests.ToString());

            _xmlWriter.WriteAttributeString("result", result.TestStatus.ToXmlResultValue(XmlResultJargon.NUnitV3));

            _xmlWriter.WriteAttributeString("time", result.Duration.ToString());

            _xmlWriter.WriteAttributeString("total", result.TotalTests.ToString());
            _xmlWriter.WriteAttributeString("passed", result.PassedTests.ToString());
            _xmlWriter.WriteAttributeString("failed", result.FailedTests.ToString());
            _xmlWriter.WriteAttributeString("inconclusive", result.InconclusiveTests.ToString());
            _xmlWriter.WriteAttributeString("skipped", result.SkippedTests.ToString());
            _xmlWriter.WriteAttributeString("asserts", result.AssertCount.ToString());

            _xmlWriter.WriteAttributeString("run-date", XmlConvert.ToString(_runStartTime, "yyyy-MM-dd"));
            _xmlWriter.WriteAttributeString("start-time", XmlConvert.ToString(_runStartTime, "HH:mm:ss"));

            _xmlWriter.WriteAttributeString("random-seed", Randomizer.InitialSeed.ToString());

            WriteEnvironmentElement();
        }

        void WriteEnvironmentElement()
        {
            if (_xmlWriter == null) // should never happen, would mean a programmers error
                throw new InvalidOperationException("Null writer");
            _xmlWriter.WriteStartElement("environment");

            var assembly = Assembly.GetExecutingAssembly();
            AssemblyName assemblyName = AssemblyHelper.GetAssemblyName(assembly);
            _xmlWriter.WriteAttributeString("nunit-version", assemblyName.Version.ToString());

            _xmlWriter.WriteAttributeString("clr-version", Environment.Version.ToString());
            _xmlWriter.WriteAttributeString("os-version", Environment.OSVersion.ToString());
            _xmlWriter.WriteAttributeString("platform", Environment.OSVersion.Platform.ToString());
            _xmlWriter.WriteAttributeString("cwd", Environment.CurrentDirectory);
            _xmlWriter.WriteAttributeString("machine-name", Environment.MachineName);
            _xmlWriter.WriteAttributeString("user", Environment.UserName);
            _xmlWriter.WriteAttributeString("user-domain", Environment.UserDomainName);
            _xmlWriter.WriteAttributeString("culture", CultureInfo.CurrentCulture.ToString());
            _xmlWriter.WriteAttributeString("uiculture", CultureInfo.CurrentUICulture.ToString());

            _xmlWriter.WriteEndElement();
        }

        void WriteResultElement(IResultSummary result)
        {
            // much simpler than in other writers, we just need to get the child nodes of each of the test-run and write
            // them. NUnit3 already gave us the xml we need to use
            foreach (var testRun in result)
            {
                for (var i = 0; i < testRun.Result.ChildNodes.Count; i++)
                {
                    var node = testRun.Result.ChildNodes[1];
                    var envNodes = node.SelectNodes(".//environment"); // we already add our own data
                    for (var j = 0; j < envNodes.Count; j++)
                    {
                        node.RemoveChild(envNodes[j]);
                    }
                    node.WriteTo(_xmlWriter);
                }
            }
        }

        void TerminateXmlFile()
        {
            if (_xmlWriter == null) // should never happen, would mean a programmers error
                throw new InvalidOperationException("Null writer");
            _xmlWriter.WriteEndElement(); // test-run
            _xmlWriter.WriteEndDocument();
            _xmlWriter.Flush();
            _xmlWriter.Close();
        }
    }
}
