// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.iOS.Shared.TestImporter.Templates.Managed;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests.TestImporter.Templates.Managed
{
    public class InfoPlistGeneratorTests
    {
        [Fact]
        public void GenerateCodeNullTemplateFile()
        {
            Assert.ThrowsAsync<ArgumentNullException>(() =>
               InfoPlistGenerator.GenerateCodeAsync(null, "Project Name"));
        }

        [Fact]
        public void GenerateCodeNullProjectName()
        {
            var tmp = Path.GetTempFileName();
            File.WriteAllText(tmp, "Hello");
            using (var stream = new FileStream(tmp, FileMode.Open))
            {
                Assert.ThrowsAsync<ArgumentNullException>(() => InfoPlistGenerator.GenerateCodeAsync(stream, null));
            }

            File.Delete(tmp);
        }

        [Fact]
        public async Task GenerateCode()
        {
            const string projectName = "MyTest";
            var fakeTemplate = $"{InfoPlistGenerator.ApplicationNameReplacement}-{InfoPlistGenerator.IndentifierReplacement}";
            var tmpPath = Path.GetTempPath();
            var templatePath = Path.Combine(tmpPath, Path.GetRandomFileName());
            using (var file = new StreamWriter(templatePath, false))
            {
                await file.WriteAsync(fakeTemplate);
            }

            var result = await InfoPlistGenerator.GenerateCodeAsync(File.OpenRead(templatePath), projectName);
            try
            {
                Assert.DoesNotContain(InfoPlistGenerator.ApplicationNameReplacement, result);
                Assert.DoesNotContain(InfoPlistGenerator.IndentifierReplacement, result);
                Assert.Contains(projectName, result);
            }
            finally
            {
                File.Delete(templatePath);
            }
        }
    }
}
