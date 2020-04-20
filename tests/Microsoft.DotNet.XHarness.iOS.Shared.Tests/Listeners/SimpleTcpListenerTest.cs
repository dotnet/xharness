﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Net.Sockets;
using Microsoft.DotNet.XHarness.iOS.Shared.Listeners;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests.Listeners
{
    public class SimpleTcpListenerTest : IDisposable
    {
        private Mock<ILog> _log;
        private Mock<ILog> _testLog;

        public SimpleTcpListenerTest()
        {
            _log = new Mock<ILog>();
            _testLog = new Mock<ILog>();
        }

        public void Dispose()
        {
            _log = null;
            _testLog = null;
        }

        [Fact]
        public void ProcessTest()
        {
            var tempResult = Path.GetTempFileName();
            // create a stream to be used and write the data there
            var lines = new string[] { "first line", "second line", "last line" };
            // setup the expected data to be written
            _testLog.Setup(l => l.Write(It.IsAny<byte[]>(), 0, It.IsAny<int>())).Callback<byte[], int, int>((buffer, start, end) =>
            {
                using (var resultStream = File.Create(tempResult))
                {// opening closing a lot, but for the test we do not care
                    resultStream.Write(buffer, start, end);
                    resultStream.Flush();
                }
            });
            // create a linstener that will start in an other thread, connect to it
            // and send the data.
            var listener = new SimpleTcpListener(_log.Object, _testLog.Object, true, true);
            listener.Initialize();
            var connectionPort = listener.Port;
            listener.StartAsync();
            // create a tcp client which will write the logs, then verity that
            // the expected data was provided
            var client = new TcpClient();
            client.Connect("localhost", connectionPort);
            using (var networkStream = client.GetStream())
            using (var streamWriter = new StreamWriter(networkStream))
            {
                foreach (var line in lines)
                {
                    streamWriter.WriteLine(line);
                    streamWriter.Flush();
                }
            }
            listener.Cancel();
            bool firstLineFound = false;
            bool secondLineFound = false;
            bool lastLineFound = false;
            // read the data in the tempResult and ensure lines are present
            using (var reader = new StreamReader(tempResult))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.EndsWith(lines[0]))
                    {
                        firstLineFound = true;
                    }

                    if (line.EndsWith(lines[1]))
                    {
                        secondLineFound = true;
                    }

                    if (line.EndsWith(lines[2]))
                    {
                        lastLineFound = true;
                    }
                }
            }
            Assert.True(firstLineFound, "first line");
            Assert.True(secondLineFound, "second line");
            Assert.True(lastLineFound, "last line");
        }
    }
}
