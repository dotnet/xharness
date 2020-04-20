// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Reflection;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests
{
    public class AppBundleInformationParserTests : IDisposable
    {
        private const string appName = "com.xamarin.bcltests.SystemXunit";
        private static readonly string outputPath = Path.GetDirectoryName(Assembly.GetAssembly(typeof(AppBundleInformationParser)).Location);
        private static readonly string sampleProjectPath = Path.Combine(outputPath, "Samples", "TestProject");
        private static readonly string appPath = Path.Combine(sampleProjectPath, "bin", appName + ".app");
        private static readonly string projectFilePath = Path.Combine(sampleProjectPath, "SystemXunit.csproj");

        public AppBundleInformationParserTests()
        {
            Directory.CreateDirectory(appPath);
        }

        public void Dispose()
        {
            Directory.Delete(appPath, true);
        }

        [Fact]
        public void InitializeTest()
        {
            var parser = new AppBundleInformationParser();

            var info = parser.ParseFromProject(projectFilePath, TestTarget.Simulator_iOS64, "Debug");

            Assert.Equal(appName, info.AppName);
            Assert.Equal(appPath, info.AppPath);
            Assert.Equal(appPath, info.LaunchAppPath);
            Assert.Equal(appName, info.BundleIdentifier);
        }
    }
}
