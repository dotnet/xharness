﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests.Logging
{
    public class LogFileTest : IDisposable
    {
        private string _path;
        private string _description;
        private Mock<ILogs> _logs;

        public LogFileTest()
        {
            _description = "My log";
            _path = Path.GetTempFileName();
            _logs = new Mock<ILogs>();
            File.Delete(_path); // delete the empty file
        }

        public void Dispose()
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }

            _path = null;
            _description = null;
            _logs = null;
        }

        [Fact]
        public void ConstructorTest()
        {
            using (var log = new LogFile(_description, _path))
            {
                Assert.Equal(_description, log.Description);
                Assert.Equal(_path, log.FullPath);
            }
        }

        [Fact]
        public void ConstructorNullPathTest()
        {
            Assert.Throws<ArgumentNullException>(() => { var log = new LogFile(_description, null); });
        }

        [Fact]
        public void ConstructorNullDescriptionTest()
        {
            using var log = new LogFile(null, _path);
        }


        [Fact]
        public void WriteTest()
        {
            string oldLine = "Hello world!";
            string newLine = "Hola mundo!";
            // create a log, write to it and assert that we have the expected data
            using (var stream = File.Create(_path))
            using (var writer = new StreamWriter(stream))
            {
                writer.WriteLine(oldLine);
            }
            using (var log = new LogFile(_description, _path))
            {
                log.WriteLine(newLine);
                log.Flush();
            }
            bool oldLineFound = false;
            bool newLineFound = false;
            using (var reader = new StreamReader(_path))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line == oldLine)
                    {
                        oldLineFound = true;
                    }

                    if (line.EndsWith(newLine)) // consider time stamp
                    {
                        newLineFound = true;
                    }
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
            using (var stream = File.Create(_path))
            using (var writer = new StreamWriter(stream))
            {
                writer.WriteLine(oldLine);
            }
            using (var log = new LogFile(_description, _path, false))
            {
                log.WriteLine(newLine);
                log.Flush();
            }
            bool oldLineFound = false;
            bool newLineFound = false;
            using (var reader = new StreamReader(_path))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line == oldLine)
                    {
                        oldLineFound = true;
                    }

                    if (line.EndsWith(newLine)) // consider timestamp
                    {
                        newLineFound = true;
                    }
                }
            }

            Assert.False(oldLineFound, "old line");
            Assert.True(newLineFound, "new line");
        }

    }
}
