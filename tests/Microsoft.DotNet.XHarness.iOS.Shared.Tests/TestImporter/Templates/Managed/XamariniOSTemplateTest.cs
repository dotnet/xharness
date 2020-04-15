﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Text;
using Microsoft.DotNet.XHarness.iOS.Shared.TestImporter;
using Microsoft.DotNet.XHarness.iOS.Shared.TestImporter.Templates;
using Microsoft.DotNet.XHarness.iOS.Shared.TestImporter.Templates.Managed;
using Moq;
using NUnit.Framework;

namespace Xharness.Tests.TestImporter.Xamarin.Tests
{

    [TestFixture]
    public class XamariniOSTemplateTest
    {

        string outputdir;
        Mock<IAssemblyLocator> assemlyLocator;
        Mock<IProjectFilter> projectFilter;
        XamariniOSTemplate template;

        [SetUp]
        public void SetUp()
        {
            outputdir = Path.GetTempFileName();
            File.Delete(outputdir);
            Directory.CreateDirectory(outputdir);
            assemlyLocator = new Mock<IAssemblyLocator>();
            projectFilter = new Mock<IProjectFilter>();
            template = new XamariniOSTemplate
            {
                AssemblyLocator = assemlyLocator.Object,
                ProjectFilter = projectFilter.Object,
                OutputDirectoryPath = outputdir,
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(outputdir))
                Directory.Delete(outputdir, true);
            outputdir = null;
            assemlyLocator = null;
            projectFilter = null;
            template = null;
        }
        [TestCase("iOSProject", Platform.iOS, "iOSProject.csproj")]
        [TestCase("WatchOSProject", Platform.WatchOS, "WatchOSProject-watchos.csproj")]
        [TestCase("TvOSProject", Platform.TvOS, "TvOSProject-tvos.csproj")]
        [TestCase("macOSProject", Platform.MacOSFull, "macOSProject-mac-full.csproj")]
        [TestCase("macOSProject", Platform.MacOSModern, "macOSProject-mac-modern.csproj")]
        public void GetProjectPathTest(string projectName, Platform platform, string expectedName)
        {
            // ignore the fact that all params are the same, we do not care
            var path = template.GetProjectPath(projectName, platform);
            Assert.AreEqual(Path.Combine(template.OutputDirectoryPath, expectedName), path);
        }

        [TestCase("WatchApp", WatchAppType.App, "WatchApp-watchos-app.csproj")]
        [TestCase("WatchExtension", WatchAppType.Extension, "WatchExtension-watchos-extension.csproj")]
        public void GetProjectPathWatchOSTest(string projectName, WatchAppType appType, string expectedName)
        {
            // ignore the fact that all params are the same, we do not care
            var path = template.GetProjectPath(projectName, appType);
            Assert.AreEqual(Path.Combine(template.OutputDirectoryPath, expectedName), path);
        }

        [TestCase("/usr/path", Platform.iOS, "Info.plist")]
        [TestCase("/usr/second/path", Platform.TvOS, "Info-tv.plist")]
        [TestCase("/usr/other/path", Platform.WatchOS, "Info-watchos.plist")]
        [TestCase("/usr/other/path", Platform.MacOSFull, "Info-mac.plist")]
        [TestCase("/usr/other/path", Platform.MacOSModern, "Info-mac.plist")]
        public void GetPListPathTest(string rootDir, Platform platform, string expectedName)
        {
            var path = XamariniOSTemplate.GetPListPath(rootDir, platform);
            Assert.AreEqual(Path.Combine(rootDir, expectedName), path);
        }

        [TestCase("/usr/bin", WatchAppType.App, "Info-watchos-app.plist")]
        [TestCase("/usr/local", WatchAppType.Extension, "Info-watchos-extension.plist")]
        public void GetPListPathWatchOSTest(string rootDir, WatchAppType appType, string expectedName)
        {
            var path = XamariniOSTemplate.GetPListPath(rootDir, appType);
            Assert.AreEqual(Path.Combine(rootDir, expectedName), path);
        }

        [TestCase("System.Xml.dll")]
        [TestCase("MyAssembly.dll")]
        public void GetReferenceNodeNullHintTest(string assembly)
        {
            var expected = $"<Reference Include=\"{assembly}\" />";
            Assert.AreEqual(expected, XamariniOSTemplate.GetReferenceNode(assembly));
        }

        [TestCase("System.Xml.dll", "my/path/to/the/dll")]
        [TestCase("MyAssembly.dll", "thepath")]
        public void GetReferenceNodeTest(string assembly, string hint)
        {
            var fixedHint = hint.Replace("/", "\\");
            var sb = new StringBuilder();
            sb.AppendLine($"<Reference Include=\"{assembly}\" >");
            sb.AppendLine($"<HintPath>{fixedHint}</HintPath>");
            sb.AppendLine("</Reference>");
            var expected = sb.ToString();
            Assert.AreEqual(expected, XamariniOSTemplate.GetReferenceNode(assembly, hint));
        }

        [TestCase("my/path/to/the/dll")]
        [TestCase("my/other/path/to/the/dll")]
        public void GetRegisterTypeNodeTest(string registerPath)
        {
            var fixedPath = registerPath.Replace("/", "\\");
            var sb = new StringBuilder();
            sb.AppendLine($"<Compile Include=\"{registerPath}\">");
            sb.AppendLine($"<Link>{Path.GetFileName(registerPath)}</Link>");
            sb.AppendLine("</Compile>");
            var expected = sb.ToString();
            Assert.AreEqual(expected, XamariniOSTemplate.GetRegisterTypeNode(registerPath));
        }

        [TestCase("/path/to/resource/my-ignore-file.ignore")]
        [TestCase("/path/to/mono/my-trait-file.txt")]
        public void GetContentNodeTest(string contentFile)
        {
            var fixedPath = contentFile.Replace("/", "\\");
            var sb = new StringBuilder();
            sb.AppendLine($"<Content Include=\"{fixedPath}\">");
            sb.AppendLine($"<Link>{Path.GetFileName(contentFile)}</Link>");
            sb.AppendLine("<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>");
            sb.AppendLine("</Content>");
            var expected = sb.ToString();
            Assert.AreEqual(expected, XamariniOSTemplate.GetContentNode(contentFile));
        }
    }
}
