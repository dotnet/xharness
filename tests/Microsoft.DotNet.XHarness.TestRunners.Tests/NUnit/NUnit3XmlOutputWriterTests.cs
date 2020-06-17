// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.DotNet.XHarness.TestRunners.NUnit;
using Moq;
using NUnit.Engine;
using Xunit;

namespace Microsoft.DotNet.XHarness.TestRunners.Tests.NUnit
{
    public class NUnit3XmlOutputWriterTests : IDisposable
    {
        private const string SampleFileName = "NUnitV3Sample.xml";
        private readonly Mock<IResultSummary> _resultSummary;
        private readonly string _tempPath;

        public NUnit3XmlOutputWriterTests()
        {
            _resultSummary = new Mock<IResultSummary>();
            _tempPath = Path.GetTempFileName();
            File.Delete(_tempPath);
        }

        private XmlNode GetTestRunSample()
        {
            string[] resourcenames = GetType().Assembly.GetManifestResourceNames();
            // load the test-run node from the sample file
            string name = GetType().Assembly
                .GetManifestResourceNames().FirstOrDefault(a => a.EndsWith(SampleFileName, StringComparison.Ordinal));
            var doc = new XmlDocument();
            using var sampleStream = new StreamReader(GetType().Assembly.GetManifestResourceStream(name));
            doc.Load(sampleStream);
            return doc.SelectNodes("test-run")[0];
        }

        [Fact]
        public void SingleTestRunTest()
        {
            var testRun = new Mock<ITestRun>();
            testRun.Setup(t => t.Result).Returns(GetTestRunSample());
            // set the expectations of the mock, the important thing, we want to return a single test-run node
            _resultSummary.Setup(rs => rs.GetEnumerator())
                .Returns(new List<ITestRun> { testRun.Object }.GetEnumerator());

            using (var writer = new StreamWriter(_tempPath))
            {
                var nunit3Writer = new NUnit3XmlOutputWriter(DateTime.Now);
                nunit3Writer.WriteResultFile(_resultSummary.Object, writer);
            }

            // read the file and make sure that is correct
            var doc = new XmlDocument();
            doc.Load(_tempPath);
            // we just need to make sure we have a single test-run node and a single env node, the rest
            // was generated by nunit
            XmlNodeList runs = doc.SelectNodes(".//test-run");
            Assert.Equal(1, runs.Count);
            XmlNodeList enviroment = doc.SelectNodes(".//environment");
            Assert.Equal(1, enviroment.Count);
        }

        [Fact]
        public void SeveralTetRunTest()
        {
            // same logic as with the other tests, but with more than one test run
            var firstTestRun = new Mock<ITestRun>();
            firstTestRun.Setup(t => t.Result).Returns(GetTestRunSample());
            var secondTestRun = new Mock<ITestRun>();
            secondTestRun.Setup(t => t.Result).Returns(GetTestRunSample());
            _resultSummary.Setup(rs => rs.GetEnumerator())
                .Returns(new List<ITestRun> { firstTestRun.Object, secondTestRun.Object }.GetEnumerator());

            using (var writer = new StreamWriter(_tempPath))
            {
                var nunit3Writer = new NUnit3XmlOutputWriter(DateTime.Now);
                nunit3Writer.WriteResultFile(_resultSummary.Object, writer);
            }

            // read the file and make sure that is correct
            var doc = new XmlDocument();
            doc.Load(_tempPath);
            XmlNodeList runs = doc.SelectNodes(".//test-run");
            Assert.Equal(1, runs.Count);
            XmlNodeList enviroment = doc.SelectNodes(".//environment");
            Assert.Equal(1, enviroment.Count);
        }

        public void Dispose() => File.Delete(_tempPath);
    }
}
