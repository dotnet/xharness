// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Runtime.InteropServices;
using Xunit;
using Microsoft.DotNet.XHarness.Tests.Runners.Xunit;

namespace Microsoft.DotNet.XHarness.Tests.Runners.Tests
{
    public class XUnitFilterTests
    {

        [Fact]
        public void CreateSingleFilterNullTestName()
        {
            Assert.Throws<ArgumentException>(() => XUnitFilter.CreateSingleFilter(null, true));
            Assert.Throws<ArgumentException>(() => XUnitFilter.CreateSingleFilter("", true));
        }

        [Theory]
        [InlineData("TestMethod", "TestAssembly", true)]
        [InlineData("TestMethod", "TestAssembly", false)]
        [InlineData("TestMethod", null, false)]
        public void CreateSingleFilter(string methodName, string assemblyName, bool excluded)
        {
            var filter = XUnitFilter.CreateSingleFilter(methodName, excluded, assemblyName);
            Assert.Equal(methodName, filter.SelectorValue);
            Assert.Equal(assemblyName, filter.AssemblyName);
            Assert.Equal(excluded, filter.Exclude);
            Assert.Equal(XUnitFilterType.Single, filter.FilterType);
        }

        [Fact]
        public void CreateAssemblyFilterNullAssemblyName()
        {
            Assert.Throws<ArgumentException>(() => XUnitFilter.CreateAssemblyFilter(null, true));
            Assert.Throws<ArgumentException>(() => XUnitFilter.CreateAssemblyFilter("", true));
        }

        [Theory]
        [InlineData("MyTestAssembly", true)]
        [InlineData("MySecondAssembly", true)]
        [InlineData("MyTestAssembly", false)]
        public void CreateAssemblyFilter(string assemblyName, bool excluded)
        {
            var filter = XUnitFilter.CreateAssemblyFilter(assemblyName, excluded);
            Assert.Null(filter.SelectorName);
            Assert.Equal(assemblyName, filter.AssemblyName);
            Assert.Equal(excluded, filter.Exclude);
            Assert.Equal(XUnitFilterType.Assembly, filter.FilterType);
        }

        [Fact]
        public void CreateNamespaceFilterNullNameSpace()
        {
            Assert.Throws<ArgumentException>(() => XUnitFilter.CreateNamespaceFilter(null, true));
            Assert.Throws<ArgumentException>(() => XUnitFilter.CreateNamespaceFilter("", true));
        }

        [Theory]
        [InlineData("MyNameSpace", "MyAssembly", true)]
        [InlineData("MyNameSpace", "MyAssembly", false)]
        [InlineData("MyNameSpace", null, false)]
        public void CreateNamespaceFilter(string nameSpace, string assemblyName, bool excluded)
        {
            var filter = XUnitFilter.CreateNamespaceFilter(nameSpace, excluded, assemblyName);
            Assert.Equal(nameSpace, filter.SelectorValue);
            Assert.Equal(assemblyName, filter.AssemblyName);
            Assert.Equal(excluded, filter.Exclude);
            Assert.Equal(XUnitFilterType.Namespace, filter.FilterType);
        }

        [Fact]
        public void CreateClassFilterNullClassName()
        {
            Assert.Throws<ArgumentException>(() => XUnitFilter.CreateClassFilter(null, true));
            Assert.Throws<ArgumentException>(() => XUnitFilter.CreateClassFilter("", true));
        }

        [Theory]
        [InlineData("MyClass", "MyAssembly", true)]
        [InlineData("MyClass", "MyAssembly", false)]
        [InlineData("MyClass", null, false)]
        public void CreateClassFilter(string className, string assemblyName, bool excluded)
        {
            var filter = XUnitFilter.CreateClassFilter(className, excluded, assemblyName);
            Assert.Equal(className, filter.SelectorValue);
            Assert.Equal(assemblyName, filter.AssemblyName);
            Assert.Equal(excluded, filter.Exclude);
            Assert.Equal(XUnitFilterType.TypeName, filter.FilterType);
        }

        [Fact]
        public void CreateTraitFilterNullTrait()
        {
            Assert.Throws<ArgumentException>(() => XUnitFilter.CreateTraitFilter(null, "value", true));
            Assert.Throws<ArgumentException>(() => XUnitFilter.CreateTraitFilter("", "value", true));
        }

        [Theory]
        [InlineData("MyTrait", "MyTraitValue", true)]
        [InlineData("MyTrait", "MyTraitValue", false)]
        [InlineData("MyTrait", null, false)]
        public void CreateTraitFilter(string trait, string traitValue, bool excluded)
        {
            var filter = XUnitFilter.CreateTraitFilter(trait, traitValue, excluded);
            Assert.Equal(trait, filter.SelectorName);
            if (traitValue == null)
            {
                Assert.Equal(string.Empty, filter.SelectorValue);
            }
            else
            {
                Assert.Equal(traitValue, filter.SelectorValue);
            }
            Assert.Null(filter.AssemblyName);
            Assert.Equal(excluded, filter.Exclude);
            Assert.Equal(XUnitFilterType.Trait, filter.FilterType);
        }

    }
}
