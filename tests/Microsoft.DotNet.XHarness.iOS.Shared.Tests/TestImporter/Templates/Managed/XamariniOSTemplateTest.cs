// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using Microsoft.DotNet.XHarness.iOS.Shared.TestImporter;
using Microsoft.DotNet.XHarness.iOS.Shared.TestImporter.Templates;
using Microsoft.DotNet.XHarness.iOS.Shared.TestImporter.Templates.Managed;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests.TestImporter.Templates.Managed
{
    public class XamariniOSTemplateTest : IDisposable
    {
        private readonly string _outputdir;
        private readonly Mock<IAssemblyLocator> _assemlyLocator;
        private readonly Mock<IProjectFilter> _projectFilter;
        private readonly XamariniOSTemplate _template;

        public XamariniOSTemplateTest()
        {
            _outputdir = Path.GetTempFileName();
            File.Delete(_outputdir);
            Directory.CreateDirectory(_outputdir);
            _assemlyLocator = new Mock<IAssemblyLocator>();
            _projectFilter = new Mock<IProjectFilter>();
            _template = new XamariniOSTemplate
            {
                AssemblyLocator = _assemlyLocator.Object,
                ProjectFilter = _projectFilter.Object,
                OutputDirectoryPath = _outputdir,
            };
        }

        public void Dispose()
        {
            if (Directory.Exists(_outputdir))
            {
                Directory.Delete(_outputdir, true);
            }
        }

        [Theory]
        [InlineData("iOSProject", Platform.iOS, "iOSProject.csproj")]
        [InlineData("WatchOSProject", Platform.WatchOS, "WatchOSProject-watchos.csproj")]
        [InlineData("TvOSProject", Platform.TvOS, "TvOSProject-tvos.csproj")]
        [InlineData("macOSProject", Platform.MacOSFull, "macOSProject-mac-full.csproj")]
        [InlineData("macOSProject", Platform.MacOSModern, "macOSProject-mac-modern.csproj")]
        public void GetProjectPathTest(string projectName, Platform platform, string expectedName)
        {
            // ignore the fact that all params are the same, we do not care
            var path = _template.GetProjectPath(projectName, platform);
            Assert.Equal(Path.Combine(_template.OutputDirectoryPath, expectedName), path);
        }

        [Theory]
        [InlineData("WatchApp", WatchAppType.App, "WatchApp-watchos-app.csproj")]
        [InlineData("WatchExtension", WatchAppType.Extension, "WatchExtension-watchos-extension.csproj")]
        public void GetProjectPathWatchOSTest(string projectName, WatchAppType appType, string expectedName)
        {
            // ignore the fact that all params are the same, we do not care
            var path = _template.GetProjectPath(projectName, appType);
            Assert.Equal(Path.Combine(_template.OutputDirectoryPath, expectedName), path);
        }

        [Theory]
        [InlineData("/usr/path", Platform.iOS, "Info.plist")]
        [InlineData("/usr/second/path", Platform.TvOS, "Info-tv.plist")]
        [InlineData("/usr/other/path", Platform.WatchOS, "Info-watchos.plist")]
        [InlineData("/usr/other/path", Platform.MacOSFull, "Info-mac.plist")]
        [InlineData("/usr/other/path", Platform.MacOSModern, "Info-mac.plist")]
        public void GetPListPathTest(string rootDir, Platform platform, string expectedName)
        {
            var path = XamariniOSTemplate.GetPListPath(rootDir, platform);
            Assert.Equal(Path.Combine(rootDir, expectedName), path);
        }

        [Theory]
        [InlineData("/usr/bin", WatchAppType.App, "Info-watchos-app.plist")]
        [InlineData("/usr/local", WatchAppType.Extension, "Info-watchos-extension.plist")]
        public void GetPListPathWatchOSTest(string rootDir, WatchAppType appType, string expectedName)
        {
            var path = XamariniOSTemplate.GetPListPath(rootDir, appType);
            Assert.Equal(Path.Combine(rootDir, expectedName), path);
        }

        [Theory]
        [InlineData("System.Xml.dll")]
        [InlineData("MyAssembly.dll")]
        public void GetReferenceNodeNullHintTest(string assembly)
        {
            var expected = $"<Reference Include=\"{assembly}\" />";
            Assert.Equal(expected, XamariniOSTemplate.GetReferenceNode(assembly));
        }

        [Theory]
        [InlineData("System.Xml.dll", "my/path/to/the/dll")]
        [InlineData("MyAssembly.dll", "thepath")]
        public void GetReferenceNodeTest(string assembly, string hint)
        {
            var fixedHint = hint.Replace("/", "\\");
            var sb = new StringBuilder();
            sb.AppendLine($"<Reference Include=\"{assembly}\" >");
            sb.AppendLine($"<HintPath>{fixedHint}</HintPath>");
            sb.AppendLine("</Reference>");
            var expected = sb.ToString();
            Assert.Equal(expected, XamariniOSTemplate.GetReferenceNode(assembly, hint));
        }

        [Theory]
        [InlineData("my/path/to/the/dll")]
        [InlineData("my/other/path/to/the/dll")]
        public void GetRegisterTypeNodeTest(string registerPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"<Compile Include=\"{registerPath}\">");
            sb.AppendLine($"<Link>{Path.GetFileName(registerPath)}</Link>");
            sb.AppendLine("</Compile>");
            var expected = sb.ToString();
            Assert.Equal(expected, XamariniOSTemplate.GetRegisterTypeNode(registerPath));
        }

        [Theory]
        [InlineData("/path/to/resource/my-ignore-file.ignore")]
        [InlineData("/path/to/mono/my-trait-file.txt")]
        public void GetContentNodeTest(string contentFile)
        {
            var fixedPath = contentFile.Replace("/", "\\");
            var sb = new StringBuilder();
            sb.AppendLine($"<Content Include=\"{fixedPath}\">");
            sb.AppendLine($"<Link>{Path.GetFileName(contentFile)}</Link>");
            sb.AppendLine("<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>");
            sb.AppendLine("</Content>");
            var expected = sb.ToString();
            Assert.Equal(expected, XamariniOSTemplate.GetContentNode(contentFile));
        }
    }
}
