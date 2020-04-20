﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Moq;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Listeners;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests.Listeners
{
    public class SimpleFileListenerTest : IDisposable
    {
        private string path;
        private Mock<ILog> testLog;
        private Mock<ILog> log;

        public SimpleFileListenerTest()
        {
            path = Path.GetTempFileName();
            testLog = new Mock<ILog>();
            log = new Mock<ILog>();
            File.Delete(path);
        }

        public void Dispose()
        {
            if (File.Exists(path))
                File.Delete(path);
            path = null;
            testLog = null;
            log = null;
        }

        [Fact]
        public void ConstructorNullPathTest()
        {
            Assert.Throws<ArgumentNullException>(() => new SimpleFileListener(null, log.Object, testLog.Object, false));
        }

        [Theory]
        [InlineData("Tests run: ", false)]
        [InlineData("<!-- the end -->", true)]
        public void TestProcess(string endLine, bool isXml)
        {
            var lines = new string[] { "first line", "second line", "last line" };
            // set mock expectations
            testLog.Setup(l => l.WriteLine("Tests have started executing"));
            testLog.Setup(l => l.WriteLine("Tests have finished executing"));
            foreach (var line in lines)
            {
                testLog.Setup(l => l.WriteLine(line));
            }
            // create a listener, set the writer and ensure that what we write in the file is present in the final path
            using (var sourceWriter = new StreamWriter(path))
            {
                var listener = new SimpleFileListener(path, log.Object, testLog.Object, isXml);
                listener.Initialize();
                listener.StartAsync();
                // write a number of lines and ensure that those are called in the mock
                sourceWriter.WriteLine("[Runner executing:");
                foreach (var line in lines)
                {
                    sourceWriter.WriteLine(line);
                    sourceWriter.Flush();
                }
                sourceWriter.WriteLine(endLine);
                listener.Cancel();
            }
            // verify that the expected lines were added
            foreach (var line in lines)
            {
                testLog.Verify(l => l.WriteLine(line), Times.Once);
            }
        }

    }
}
