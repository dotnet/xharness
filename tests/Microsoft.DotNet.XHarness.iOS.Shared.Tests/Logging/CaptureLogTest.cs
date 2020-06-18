// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests.Logging
{
    public class CaptureLogTest : IDisposable
    {
        private readonly string _filePath;
        private readonly string _capturePath;

        public CaptureLogTest()
        {
            _filePath = Path.GetTempFileName();
            _capturePath = Path.GetTempFileName();
            File.Delete(_filePath);
            File.Delete(_capturePath);
        }

        public void Dispose()
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }

        [Fact]
        public void ConstructorNullFilePath() => Assert.Throws<ArgumentNullException>(() =>
                                               {
                                                   var captureLog = new CaptureLog(null, _filePath, false);
                                               });


        [Fact]
        public void CaptureRightOrder()
        {
            var ignoredLine = "This lines should not be captured";
            var logLines = new[] { "first line", "second line", "thrid line" };
            using (var stream = File.Create(_filePath))
            using (var writer = new StreamWriter(stream))
            {
                writer.WriteLine(ignoredLine);
            }
            using (var captureLog = new CaptureLog(_capturePath, _filePath, false))
            {
                captureLog.StartCapture();
                using (var writer = new StreamWriter(_filePath))
                {
                    foreach (var line in logLines)
                    {
                        writer.WriteLine(line);
                    }
                }
                captureLog.StopCapture();
                // get the stream and assert we do have the correct lines
                using (var captureStream = captureLog.GetReader())
                {
                    string line;
                    while ((line = captureStream.ReadLine()) != null)
                    {
                        Assert.Contains(line, logLines);
                    }
                }
            }
        }

        [Fact]
        public void CaptureMissingFileTest()
        {
            using (var captureLog = new CaptureLog(_capturePath, _filePath, false))
            {
                Assert.Equal(_capturePath, captureLog.FullPath);
                captureLog.StartCapture();
                captureLog.StopCapture();
            }
            // read the data that was added to the capture path and  ensure that we do have the name of the missing file
            using (var reader = new StreamReader(_capturePath))
            {
                var line = reader.ReadLine();
                Assert.Contains(_filePath, line);
            }
        }

        [Fact]
        public void CaptureWrongOrder() => Assert.Throws<InvalidOperationException>(() =>
                                         {
                                             using (var captureLog = new CaptureLog(_capturePath, _filePath, false))
                                             {
                                                 captureLog.StopCapture();
                                             }
                                         });

        [Fact]
        public void CaptureWrongOrderEntirePath()
        {
            using (var captureLog = new CaptureLog(_capturePath, _filePath, true))
            {
                captureLog.StopCapture();
            }
        }
    }
}
