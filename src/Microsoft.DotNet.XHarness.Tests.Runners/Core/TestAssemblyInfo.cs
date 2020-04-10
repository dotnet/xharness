using System;
using System.Reflection;

namespace Microsoft.DotNet.XHarness.Tests.Runners.Core
{
    public class TestAssemblyInfo
    {
        public Assembly Assembly { get; }
        public string FullPath { get; }

        public TestAssemblyInfo(Assembly assembly, string fullPath)
        {
            Assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
            FullPath = fullPath ?? string.Empty;
        }
    }
}