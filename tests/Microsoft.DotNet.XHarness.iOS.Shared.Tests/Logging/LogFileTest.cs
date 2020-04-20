// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Moq;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests.Logging
{
    public class LogFileTest : IDisposable
    {
        private string path;
        private string description;
        private Mock<ILogs> logs;

        public LogFileTest()
        {
            description = "My log";
            path = Path.GetTempFileName();
            logs = new Mock<ILogs>();
            File.Delete(path); // delete the empty file
        }

        public void Dispose()
        {
            if (File.Exists(path))
                File.Delete(path);
            path = null;
            description = null;
            logs = null;
        }

        [Fact]
        public void ConstructorTest()
        {
            using (var log = new LogFile(description, path))
            {
                Assert.Equal(description, log.Description);
                Assert.Equal(path, log.FullPath);
            }
        }

        [Fact]
        public void ConstructorNullPathTest()
        {
            Assert.Throws<ArgumentNullException>(() => { var log = new LogFile(description, null); });
        }

        [Fact]
        public void ConstructorNullDescriptionTest()
        {
            using var log = new LogFile(null, path);
        }


        [Fact]
        public void WriteTest()
        {
            string oldLine = "Hello world!";
            string newLine = "Hola mundo!";
            // create a log, write to it and assert that we have the expected data
            using (var stream = File.Create(path))
            using (var writer = new StreamWriter(stream))
            {
                writer.WriteLine(oldLine);
            }
            using (var log = new LogFile(description, path))
            {
                log.WriteLine(newLine);
                log.Flush();
            }
            bool oldLineFound = false;
            bool newLineFound = false;
            using (var reader = new StreamReader(path))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line == oldLine)
                        oldLineFound = true;
                    if (line.EndsWith(newLine)) // consider time stamp
                        newLineFound = true;
                }
            }

            Assert.True(oldLineFound, "old line");
            Assert.True(newLineFound, "new line");
        }

        [Fact]
        public void WriteNotAppendTest()
        {
            string oldLine = "Hello world!";
            string newLine = "Hola mundo!";
            // create a log, write to it and assert that we have the expected data
            using (var stream = File.Create(path))
            using (var writer = new StreamWriter(stream))
            {
                writer.WriteLine(oldLine);
            }
            using (var log = new LogFile(description, path, false))
            {
                log.WriteLine(newLine);
                log.Flush();
            }
            bool oldLineFound = false;
            bool newLineFound = false;
            using (var reader = new StreamReader(path))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line == oldLine)
                        oldLineFound = true;
                    if (line.EndsWith(newLine)) // consider timestamp
                        newLineFound = true;
                }
            }

            Assert.False(oldLineFound, "old line");
            Assert.True(newLineFound, "new line");
        }

    }
}
