// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.DotNet.XHarness.iOS.Shared.TestImporter;

namespace Microsoft.DotNet.XHarness.iOS.TestImporter
{
    public enum TestingFramework
    {
        Unknown,
        NUnit,
        xUnit,
    }

    public class AssemblyDefinitionFactory : ITestAssemblyDefinitionFactory
    {
        private readonly TestingFramework _testingFramework;
        public IAssemblyLocator AssemblyLocator { get; private set; }

        public AssemblyDefinitionFactory(TestingFramework testingFramework, IAssemblyLocator assemblyLocator)
        {
            _testingFramework = testingFramework;
            if (_testingFramework == TestingFramework.Unknown)
                throw new ArgumentOutOfRangeException(nameof(_testingFramework));
            AssemblyLocator = assemblyLocator ?? throw new ArgumentNullException(nameof(assemblyLocator));
        }

        public ITestAssemblyDefinition Create(string assembly, IAssemblyLocator loader) =>
            new TestAssemblyDefinition(assembly, loader, _testingFramework == TestingFramework.xUnit);
    }

    public class TestAssemblyDefinition : ITestAssemblyDefinition
    {
        public string Name { get; set; }
        public bool IsXUnit { get; set; }
        public IAssemblyLocator AssemblyLocator { get; set; }

        public TestAssemblyDefinition(string assemblyFullPath, IAssemblyLocator assemblyLocator, bool isXunit)
        {
            if (string.IsNullOrEmpty(assemblyFullPath))
            {
                throw new ArgumentException(nameof(assemblyFullPath));
            }

            Name = Path.GetFileName(assemblyFullPath);
            AssemblyLocator = assemblyLocator ?? throw new ArgumentNullException(nameof(assemblyLocator));
            IsXUnit = isXunit;
        }

        public string GetName(Platform _) => Name;

        public string GetPath(Platform platform) => AssemblyLocator.GetHintPathForReferenceAssembly(Name, platform);
    }
}
