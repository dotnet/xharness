// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.XHarness.iOS.Shared.TestImporter;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests.TestImporter
{
    public class ProjectDefinitionTests
    {
        private readonly Mock<IAssemblyLocator> _assemblyLocator;
        private readonly Mock<ITestAssemblyDefinitionFactory> _factory;

        public ProjectDefinitionTests()
        {
            _assemblyLocator = new Mock<IAssemblyLocator>();
            _factory = new Mock<ITestAssemblyDefinitionFactory>();
        }

        [Fact]
        public void GetTypeForAssembliesNullMonoPath()
        {
            var projectDefinition = new ProjectDefinition("MyProject", _assemblyLocator.Object, _factory.Object, new List<ITestAssemblyDefinition>(), "");
            Assert.Throws<ArgumentNullException>(() => projectDefinition.GetTypeForAssemblies(null, Platform.iOS));
        }
    }
}
