// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Reflection;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests
{
    public class AppBundleInformationParserTests : IDisposable
    {
        private const string AppName = "com.xamarin.bcltests.SystemXunit";
        private static readonly string s_outputPath = Path.GetDirectoryName(Assembly.GetAssembly(typeof(AppBundleInformationParser)).Location);
        private static readonly string s_sampleProjectPath = Path.Combine(s_outputPath, "Samples", "TestProject");
        private static readonly string s_appPath = Path.Combine(s_sampleProjectPath, "bin", AppName + ".app");
        private static readonly string s_projectFilePath = Path.Combine(s_sampleProjectPath, "SystemXunit.csproj");

        public AppBundleInformationParserTests()
        {
            Directory.CreateDirectory(s_appPath);
        }

        public void Dispose()
        {
            Directory.Delete(s_appPath, true);
        }

        [Fact]
        public void InitializeTest()
        {
            var parser = new AppBundleInformationParser(Mock.Of<IProcessManager>());

            var info = parser.ParseFromProject(s_projectFilePath, TestTarget.Simulator_iOS64, "Debug");

            Assert.Equal(AppName, info.AppName);
            Assert.Equal(s_appPath, info.AppPath);
            Assert.Equal(s_appPath, info.LaunchAppPath);
            Assert.Equal(AppName, info.BundleIdentifier);
        }
    }
}
