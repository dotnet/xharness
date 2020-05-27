// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Listeners;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests.Listeners
{
    public class SimpleFileListenerTest : IDisposable
    {
        private readonly string _path;
        private readonly Mock<IFileBackedLog> _testLog;
        private readonly Mock<ILog> _log;

        public SimpleFileListenerTest()
        {
            _path = Path.GetTempFileName();
            _testLog = new Mock<IFileBackedLog>();
            _log = new Mock<ILog>();
            File.Delete(_path);
        }

        public void Dispose()
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }

        [Fact]
        public void ConstructorNullPathTest()
        {
            Assert.Throws<ArgumentNullException>(() => new SimpleFileListener(null, _log.Object, _testLog.Object, false));
        }

        [Theory]
        [InlineData("Tests run: ", false)]
        [InlineData("<!-- the end -->", true)]
        public void TestProcess(string endLine, bool isXml)
        {
            var lines = new[] { "first line", "second line", "last line" };
            // set mock expectations
            _testLog.Setup(l => l.WriteLine("Tests have started executing"));
            _testLog.Setup(l => l.WriteLine("Tests have finished executing"));
            foreach (var line in lines)
            {
                _testLog.Setup(l => l.WriteLine(line));
            }
            // create a listener, set the writer and ensure that what we write in the file is present in the final path
            using (var sourceWriter = new StreamWriter(_path))
            {
                var listener = new SimpleFileListener(_path, _log.Object, _testLog.Object, isXml);
                listener.InitializeAndGetPort();
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
                _testLog.Verify(l => l.WriteLine(line), Times.Once);
            }
        }

    }
}
