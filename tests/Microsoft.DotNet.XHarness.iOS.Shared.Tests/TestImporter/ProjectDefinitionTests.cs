// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.XHarness.iOS.Shared.TestImporter;
using Moq;
using NUnit.Framework;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests.TestImporter
{
    public class ProjectDefinitionTests
    {

        Mock<IAssemblyLocator> assemblyLocator;
        Mock<ITestAssemblyDefinitionFactory> factory;

        [SetUp]
        public void SetUp()
        {
            assemblyLocator = new Mock<IAssemblyLocator>();
            factory = new Mock<ITestAssemblyDefinitionFactory>();
        }

        [TearDown]
        public void TearDown()
        {
            assemblyLocator = null;
        }

        [Test]
        public void GetTypeForAssembliesNullMonoPath()
        {
            var projectDefinition = new ProjectDefinition("MyProject", assemblyLocator.Object, factory.Object, new List<ITestAssemblyDefinition>(), "");
            Assert.Throws<ArgumentNullException>(() => projectDefinition.GetTypeForAssemblies(null, Platform.iOS));
        }
    }
}
