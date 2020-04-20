// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests.Logging
{
    public class LogsTest : IDisposable
    {
        private readonly string directory;
        private string fileName;
        private readonly string description;

        public LogsTest()
        {
            directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            fileName = "test-file.txt";
            description = "My description";

            Directory.CreateDirectory(directory);
        }

        public void Dispose()
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }

        [Fact]
        public void ConstructorTest()
        {
            using (var logs = new Logs(directory))
            {
                Assert.Equal(directory, logs.Directory);
            }
        }

        [Fact]
        public void ConstructorNullDirTest()
        {
            Assert.Throws<ArgumentNullException>(() => new Logs(null));
        }

        [Fact]
        public void CreateFileTest()
        {
            using (var logs = new Logs(directory))
            {
                var file = logs.CreateFile(fileName, description);
                Assert.True(File.Exists(file), "exists");
                Assert.Equal(fileName, Path.GetFileName(file));
                Assert.Single(logs);
            }
        }

        [Fact]
        public void CreateFileNullPathTest()
        {
            using (var logs = new Logs(directory))
            {
                fileName = null;
                var description = "My description";
                Assert.Throws<ArgumentNullException>(() => logs.CreateFile(fileName, description));
            }
        }

        [Fact]
        public void CreateFileNullDescriptionTest()
        {
            using (var logs = new Logs(directory))
            {
                string description = null;
                logs.CreateFile(fileName, description);
                Assert.Single(logs);
            }
        }

        [Fact]
        public void AddFileTest()
        {
            var fullPath = Path.Combine(directory, fileName);
            File.WriteAllText(fullPath, "foo");

            using (var logs = new Logs(directory))
            {
                var fileLog = logs.AddFile(fullPath, description);
                Assert.Equal(fullPath, fileLog.FullPath); // path && fullPath are the same
                Assert.Equal(Path.Combine(directory, fileName), fileLog.FullPath);
                Assert.Equal(description, fileLog.Description);
            }
        }

        [Fact]
        public void AddFileNotInDirTest()
        {
            var dir1 = Path.Combine(directory, "dir1");
            var dir2 = Path.Combine(directory, "dir2");

            Directory.CreateDirectory(dir1);
            Directory.CreateDirectory(dir2);

            var filePath = Path.Combine(dir1, "test-file.txt");
            File.WriteAllText(filePath, "Hello world!");

            using (var logs = new Logs(dir2))
            {
                var newPath = Path.Combine(dir2, Path.GetFileNameWithoutExtension(fileName));
                var fileLog = logs.AddFile(filePath, description);
                Assert.StartsWith(newPath, fileLog.FullPath); // assert new path
                Assert.True(File.Exists(fileLog.FullPath), "copy");
            }
        }

        [Fact]
        public void AddFilePathNullTest()
        {
            using (var logs = new Logs(directory))
            {
                Assert.Throws<ArgumentNullException>(() => logs.AddFile(null, description));
            }
        }

        [Fact]
        public void AddFileDescriptionNull()
        {
            var fullPath = Path.Combine(directory, fileName);
            File.WriteAllText(fullPath, "foo");
            using (var logs = new Logs(directory))
            {
                logs.Create(fullPath, null);
                Assert.Single(logs);
            }
        }
    }
}
