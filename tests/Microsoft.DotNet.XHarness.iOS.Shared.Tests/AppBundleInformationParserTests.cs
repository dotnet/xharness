// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests
{
    [TestFixture]
    public class AppBundleInformationParserTests
    {

        const string appName = "com.xamarin.bcltests.SystemXunit";

        static readonly string outputPath = Path.GetDirectoryName(Assembly.GetAssembly(typeof(AppBundleInformationParser)).Location);
        static readonly string sampleProjectPath = Path.Combine(outputPath, "Samples", "TestProject");
        static readonly string appPath = Path.Combine(sampleProjectPath, "bin", appName + ".app");
        static readonly string projectFilePath = Path.Combine(sampleProjectPath, "SystemXunit.csproj");

        [SetUp]
        public void SetUp()
        {
            Directory.CreateDirectory(appPath);
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(appPath, true);
        }

        [Test]
        public void InitializeTest()
        {
            var parser = new AppBundleInformationParser();

            var info = parser.ParseFromProject(projectFilePath, TestTarget.Simulator_iOS64, "Debug");

            Assert.AreEqual(appName, info.AppName);
            Assert.AreEqual(appPath, info.AppPath);
            Assert.AreEqual(appPath, info.LaunchAppPath);
            Assert.AreEqual(appName, info.BundleIdentifier);
        }
    }
}
