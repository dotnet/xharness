﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests
{
    public class XmlResultParserTests
    {
        private static readonly Dictionary<XmlResultJargon, Action<string, string, string, string, string, string, string, int>> s_validationMap = new Dictionary<XmlResultJargon, Action<string, string, string, string, string, string, string, int>>
        {
            [XmlResultJargon.NUnitV2] = ValidateNUnitV2Failure,
            [XmlResultJargon.NUnitV3] = ValidateNUnitV3Failure,
            [XmlResultJargon.xUnit] = ValidatexUnitFailure,
        };

        private readonly XmlResultParser _resultParser;

        public XmlResultParserTests()
        {
            _resultParser = new XmlResultParser();
        }

        private string CreateResultSample(XmlResultJargon jargon, bool includePing = false)
        {
            string sampleFileName = null;
            switch (jargon)
            {
                case XmlResultJargon.NUnitV2:
                    sampleFileName = "NUnitV2Sample.xml";
                    break;
                case XmlResultJargon.NUnitV3:
                    sampleFileName = "NUnitV3Sample.xml";
                    break;
                case XmlResultJargon.TouchUnit:
                    sampleFileName = "TouchUnitSample.xml";
                    break;
                case XmlResultJargon.xUnit:
                    sampleFileName = "xUnitSample.xml";
                    break;
            }
            Assert.NotNull(sampleFileName);
            var name = GetType().Assembly.GetManifestResourceNames().Where(a => a.EndsWith(sampleFileName, StringComparison.Ordinal)).FirstOrDefault();
            var tempPath = Path.GetTempFileName();
            using (var outputStream = new StreamWriter(tempPath))
            using (var sampleStream = new StreamReader(GetType().Assembly.GetManifestResourceStream(name)))
            {
                if (includePing)
                {
                    outputStream.WriteLine("ping");
                }
                string line;
                while ((line = sampleStream.ReadLine()) != null)
                {
                    outputStream.WriteLine(line);
                }
            }
            return tempPath;
        }

        [Fact]
        public void IsValidXmlMissingFileTest()
        {
            var path = Path.GetTempFileName();
            File.Delete(path);
            Assert.False(_resultParser.IsValidXml(path, out var jargon), "missing file");
        }

        [Theory]
        [InlineData(XmlResultJargon.NUnitV2)]
        [InlineData(XmlResultJargon.NUnitV3)]
        [InlineData(XmlResultJargon.TouchUnit)]
        [InlineData(XmlResultJargon.xUnit)]
        public void IsValidXmlTest(XmlResultJargon jargon)
        {
            var path = CreateResultSample(jargon);
            Assert.True(_resultParser.IsValidXml(path, out var resultJargon), "is valid");
            Assert.Equal(jargon, resultJargon);
            File.Delete(path);
        }


        [Theory]
        [InlineData("nunit-", XmlResultJargon.NUnitV2)]
        [InlineData("nunit-", XmlResultJargon.TouchUnit)]
        [InlineData("xunit-", XmlResultJargon.xUnit)]
        public void GetXmlFilePathTest(string prefix, XmlResultJargon jargon)
        {
            var orignialPath = "/path/to/a/xml/result.xml";
            var xmlPath = _resultParser.GetXmlFilePath(orignialPath, jargon);
            var fileName = Path.GetFileName(xmlPath);
            Assert.StartsWith(prefix, fileName);
        }

        [Theory]
        [InlineData(XmlResultJargon.NUnitV3)]
        [InlineData(XmlResultJargon.NUnitV2)]
        [InlineData(XmlResultJargon.xUnit)]
        public void CleanXmlPingTest(XmlResultJargon jargon)
        {
            var path = CreateResultSample(jargon, includePing: true);
            var cleanPath = path + "_clean";
            _resultParser.CleanXml(path, cleanPath);
            Assert.True(_resultParser.IsValidXml(cleanPath, out var resultJargon), "is valid");
            Assert.Equal(jargon, resultJargon);
            File.Delete(path);
            File.Delete(cleanPath);
        }

        [Fact]
        public void CleanXmlTouchUnitTest()
        {
            // similar to CleanXmlPingTest but using TouchUnit, so we do not want to see the extra nodes
            var path = CreateResultSample(XmlResultJargon.TouchUnit, includePing: true);
            var cleanPath = path + "_clean";
            _resultParser.CleanXml(path, cleanPath);
            Assert.True(_resultParser.IsValidXml(cleanPath, out var resultJargon), "is valid");
            Assert.Equal(XmlResultJargon.NUnitV2, resultJargon);
            // load the xml, ensure we do not have the nodes we removed
            var doc = XDocument.Load(cleanPath);
            Assert.False(doc.Descendants().Where(e => e.Name == "TouchUnitTestRun").Any(), "TouchUnitTestRun");
            Assert.False(doc.Descendants().Where(e => e.Name == "NUnitOutput").Any(), "NUnitOutput");
            File.Delete(path);
            File.Delete(cleanPath);
        }

        [Fact]
        public void UpdateMissingDataTest() // only works with NUnitV3
        {
            string appName = "TestApp";
            var path = CreateResultSample(XmlResultJargon.NUnitV3);
            var cleanPath = path + "_clean";
            _resultParser.CleanXml(path, cleanPath);
            var updatedXml = path + "_updated";
            var logs = new[] { "/first/path", "/second/path", "/last/path" };
            _resultParser.UpdateMissingData(cleanPath, updatedXml, appName, logs);
            // assert that the required info was updated
            Assert.True(File.Exists(updatedXml), "file exists");
            var doc = XDocument.Load(updatedXml);
            var testSuiteElements = doc.Descendants().Where(e => e.Name == "test-suite" && e.Attribute("type")?.Value == "Assembly");
            // assert root node contains the attachments
            var rootNode = testSuiteElements.FirstOrDefault();
            Assert.NotNull(rootNode);
            var attachments = rootNode.Descendants().Where(e => e.Name == "attachment");
            var failureCount = rootNode.Descendants().Where(e => e.Name == "test-case" && e.Attribute("result").Value == "Failed").Count();
            Assert.Equal(logs.Length * (failureCount + 1), attachments.Count());

            // assert that name and full name are present and are the app name
            foreach (var node in testSuiteElements)
            {
                Assert.Equal(appName, node.Attribute("name").Value);
                Assert.Equal(appName, node.Attribute("fullname").Value);
            }
            File.Delete(path);
            File.Delete(cleanPath);
            File.Delete(updatedXml);
        }

        [Fact]
        public void GetVSTSFileNameTest()
        {
            var path = Path.GetTempFileName();
            var newPath = XmlResultParser.GetVSTSFilename(path);
            Assert.StartsWith("vsts-", Path.GetFileName(newPath));
            File.Delete(path);
        }

        private static void ValidateNUnitV2Failure(string src, string appName, string variation, string title, string message, string stderrMessage, string xmlPath, int _)
        {
            // load the doc and ensure that all the data is correct setup
            var doc = XDocument.Load(xmlPath);
            var testResultsNodes = doc.Descendants().Where(e => e.Name == "test-results");
            Assert.Single(testResultsNodes);
            var rootNode = testResultsNodes.FirstOrDefault();
            Assert.Equal(title, rootNode.Attribute("name").Value);
            Assert.Equal("1", rootNode.Attribute("total").Value);
            Assert.Equal("0", rootNode.Attribute("errors").Value);
            Assert.Equal("1", rootNode.Attribute("failures").Value);
            // ensure we do have a test result with the failure data
            var testResult = doc.Descendants().Where(e => e.Name == "test-suite" && e.Attribute("type").Value == "TestFixture");
            Assert.Single(testResult);
        }

        private static void ValidateNUnitV3Failure(string src, string appName, string variation, string title, string message, string stderrMessage, string xmlPath, int attachemntsCount)
        {
            var doc = XDocument.Load(xmlPath);
            // get test-run and verify attrs
            var testResultNodes = doc.Descendants().Where(e => e.Name == "test-run");
            Assert.Single(testResultNodes);
            var testResultNode = testResultNodes.FirstOrDefault();
            Assert.Equal(title, testResultNode.Attribute("name").Value);
            Assert.Equal("1", testResultNode.Attribute("testcasecount").Value);
            Assert.Equal("Failed", testResultNode.Attribute("result").Value);
            Assert.Equal("1", testResultNode.Attribute("total").Value);
            Assert.Equal("0", testResultNode.Attribute("passed").Value);
            Assert.Equal("1", testResultNode.Attribute("failed").Value);
            Assert.Equal("1", testResultNode.Attribute("asserts").Value);
            // important attrs for the import, if they miss, we wont be able to add the files to vsts
            Assert.NotNull(testResultNode.Attribute("run-date").Value);
            Assert.NotNull(testResultNode.Attribute("start-time").Value);
            // get the test-suite and verify the name and fullname are correct
            var testSuite = testResultNode.Descendants().Where(e => e.Name == "test-suite" && e.Attribute("type").Value == "TestFixture").FirstOrDefault();
            Assert.NotNull(testSuite);
            Assert.Equal(title, testSuite.Attribute("name").Value);
            Assert.Equal(title, testSuite.Attribute("fullname").Value);
            // verify the test case
            var testCase = testSuite.Descendants().Where(e => e.Name == "test-case").FirstOrDefault();
            Assert.NotNull(testCase);
            Assert.Equal("Failed", testCase.Attribute("result").Value);
            // validate that we do have attachments
            var attachmentsNode = testCase.Descendants().Where(e => e.Name == "attachments").FirstOrDefault();
            Assert.NotNull(attachmentsNode);
            var attachments = attachmentsNode.Descendants().Where(e => e.Name == "attachment");
            Assert.Equal(attachemntsCount, attachments.Count());
        }

        private static void ValidatexUnitFailure(string src, string appName, string variation, string title, string message, string stderrMessage, string xmlPath, int _)
        {
            var doc = XDocument.Load(xmlPath);
            // get the assemlby and validate its attrs
            var assemblies = doc.Descendants().Where(e => e.Name == "assembly");
            Assert.Single(assemblies);
            var assemblyNode = assemblies.FirstOrDefault();
            Assert.Equal(title, assemblyNode.Attribute("name").Value);
            Assert.Equal("1", assemblyNode.Attribute("total").Value);
            Assert.Equal("1", assemblyNode.Attribute("failed").Value);
            Assert.Equal("0", assemblyNode.Attribute("passed").Value);
            var collections = assemblyNode.Descendants().Where(e => e.Name == "collection");
            Assert.Single(collections);
            var collectionNode = collections.FirstOrDefault();
            // assert the collection attrs
            Assert.Equal("1", collectionNode.Attribute("failed").Value);
            Assert.Equal("0", collectionNode.Attribute("passed").Value);
        }

        [Theory]
        [InlineData(XmlResultJargon.NUnitV2)]
        [InlineData(XmlResultJargon.NUnitV3)]
        [InlineData(XmlResultJargon.xUnit)]
        public void GenerateFailureTest(XmlResultJargon jargon)
        {
            var src = "test-case";
            var appName = "MyUnitTest";
            var variation = "Debug";
            var title = "Testing";
            var message = "This is a test";
            var stderrMessage = "Something went very wrong";

            var stderrPath = Path.GetTempFileName();

            // write the message in the stderrParh that should be read
            using (var writer = new StreamWriter(stderrPath))
            {
                writer.WriteLine(stderrMessage);
            }

            // create a path with data in it
            var logs = new Mock<ILogs>();
            var tmpLogMock = new Mock<ILog>();
            var xmlLogMock = new Mock<ILog>();

            var tmpPath = Path.GetTempFileName();
            var finalPath = Path.GetTempFileName();

            // create a number of fake logs to be added to the failure
            var logsDir = Path.GetTempFileName();
            File.Delete(logsDir);
            Directory.CreateDirectory(logsDir);
            var failureLogs = new[] { "first.txt", "second.txt", "last.txt" };

            foreach (var file in failureLogs)
            {
                var path = Path.Combine(logsDir, file);
                File.WriteAllText(path, "");
            }

            // expect the creation of the two diff xml file logs
            _ = logs.Setup(l => l.Create(It.IsAny<string>(), "Failure Log tmp", null)).Returns(tmpLogMock.Object);
            _ = logs.Setup(l => l.Create(It.IsAny<string>(), LogType.XmlLog.ToString(), null)).Returns(xmlLogMock.Object);
            if (jargon == XmlResultJargon.NUnitV3)
            {
                _ = logs.Setup(l => l.Directory).Returns(logsDir);
                _ = tmpLogMock.Setup(tmpLog => tmpLog.FullPath).Returns(tmpPath);

            }

            // return the two temp files so that we can later validate that everything is present
            _ = xmlLogMock.Setup(xmlLog => xmlLog.FullPath).Returns(finalPath);

            _resultParser.GenerateFailure(logs.Object, src, appName, variation, title, message, stderrPath, jargon);

            // actual assertions do happen in the validation functions
            s_validationMap[jargon](src, appName, variation, title, message, stderrMessage, finalPath, failureLogs.Length);

            // verify that we are correctly adding the logs
            logs.Verify(l => l.Create(It.IsAny<string>(), It.IsAny<string>(), null), jargon == XmlResultJargon.NUnitV3 ? Times.AtMost(2) : Times.AtMostOnce());
            if (jargon == XmlResultJargon.NUnitV3)
            {
                logs.Verify(l => l.Directory, Times.Once);
                tmpLogMock.Verify(l => l.FullPath, Times.AtLeastOnce);

            }

            xmlLogMock.Verify(l => l.FullPath, Times.AtLeastOnce);

            // clean files
            File.Delete(stderrPath);
            File.Delete(tmpPath);
            File.Delete(finalPath);
            Directory.Delete(logsDir, true);
        }

        /// <summary>
        /// https://github.com/xamarin/xamarin-macios/issues/8214
        /// </summary>
        [Fact]
        public void Issue8214Test()
        {
            string expectedResultLine = "Tests run: 2376 Passed: 2301 Inconclusive: 13 Failed: 1 Ignored: 74";
            // get the sample that was added to the issue to validate that we do parse the resuls correctly and copy it to a local
            // path to be parsed
            var name = GetType().Assembly.GetManifestResourceNames().Where(a => a.EndsWith("Issue8214.xml", StringComparison.Ordinal)).FirstOrDefault();
            var tempPath = Path.GetTempFileName();
            var destinationFile = Path.GetTempFileName();
            using (var outputStream = new StreamWriter(tempPath))
            using (var sampleStream = new StreamReader(GetType().Assembly.GetManifestResourceStream(name)))
            {
                string line;
                while ((line = sampleStream.ReadLine()) != null)
                {
                    outputStream.WriteLine(line);
                }
            }
            var (resultLine, failed) = _resultParser.ParseResults(tempPath, destinationFile, XmlResultJargon.NUnitV3, true);
            Assert.True(failed, "failed");
            Assert.Equal(expectedResultLine, resultLine);
            // verify that the destination does contain the result line
            string resultLineInDestinationFile = null;
            using (var resultReader = new StreamReader(destinationFile))
            {
                string line;
                while ((line = resultReader.ReadLine()) != null)
                {
                    if (line.Contains("Tests run:"))
                    {
                        resultLineInDestinationFile = line;
                        break;
                    }
                }
            }
            Assert.NotNull(resultLineInDestinationFile);
            Assert.Equal(expectedResultLine, resultLineInDestinationFile);
        }

        /// <summary>
        /// 
        /// </summary>
        [Fact]
        public void DoNotGenerateHtmlReport()
        {
            string expectedResultLine = "Tests run: 2376 Passed: 2301 Inconclusive: 13 Failed: 1 Ignored: 74";
            // get the sample that was added to the issue to validate that we do parse the resuls correctly and copy it to a local
            // path to be parsed
            var name = GetType().Assembly.GetManifestResourceNames().Where(a => a.EndsWith("Issue8214.xml", StringComparison.Ordinal)).FirstOrDefault();
            var tempPath = Path.GetTempFileName();
            var destinationFile = Path.GetTempFileName();
            if (File.Exists(destinationFile))
                File.Delete(destinationFile);
            using (var outputStream = new StreamWriter(tempPath))
            using (var sampleStream = new StreamReader(GetType().Assembly.GetManifestResourceStream(name)))
            {
                string line;
                while ((line = sampleStream.ReadLine()) != null)
                {
                    outputStream.WriteLine(line);
                }
            }
            var (resultLine, failed) = _resultParser.ParseResults(tempPath, destinationFile, XmlResultJargon.NUnitV3, false);
            Assert.True(failed, "failed");
            Assert.Equal(expectedResultLine, resultLine);
            // verify that the file in the destination was not created
            Assert.False(File.Exists(destinationFile));
        }
    }
}
