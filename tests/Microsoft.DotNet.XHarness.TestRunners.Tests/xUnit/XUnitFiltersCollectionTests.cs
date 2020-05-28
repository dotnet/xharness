// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.DotNet.XHarness.TestRunners.Common;
using Microsoft.DotNet.XHarness.TestRunners.Xunit;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.XHarness.TestRunners.Tests.xUnit
{
    public class XUnitFiltersCollectionTests
    {

        public class FiltersTestData
        {
            public static IEnumerable<object[]> TestCaseFilters
            {
                get
                {
                    // single filter that excludes
                    var testDisplayName = "MyNameSpace.MyClassTest.TestThatFooEqualsBat";
                    // match and exclude
                    var filter = XUnitFilter.CreateSingleFilter(
                        singleTestName: testDisplayName,
                        exclude: true);
                    var collection = new XUnitFiltersCollection { filter };
                    var testCase = new Mock<ITestCase>();
                    testCase.Setup(t => t.DisplayName).Returns(testDisplayName);

                    yield return new object[]
                    {
                        collection,
                        testCase.Object,
                        true,
                    };

                    // single filter that includes the test case in the run
                    filter = XUnitFilter.CreateSingleFilter(
                        singleTestName: testDisplayName,
                        exclude: false);
                    collection = new XUnitFiltersCollection { filter };
                    testCase = new Mock<ITestCase>();
                    testCase.Setup(t => t.DisplayName).Returns(testDisplayName);

                    yield return new object[]
                    {
                        collection,
                        testCase.Object,
                        false,
                    };

                    // two excluding filters
                    filter = XUnitFilter.CreateSingleFilter(
                        singleTestName: testDisplayName,
                        exclude: true);
                    var filter2 = XUnitFilter.CreateSingleFilter(
                        singleTestName: testDisplayName,
                        exclude: true);
                    collection = new XUnitFiltersCollection { filter, filter2};
                    testCase = new Mock<ITestCase>();
                    testCase.Setup(t => t.DisplayName).Returns(testDisplayName);

                    yield return new object[]
                    {
                        collection,
                        testCase.Object,
                        true,
                    };

                    // two including filters
                    filter = XUnitFilter.CreateSingleFilter(
                        singleTestName: testDisplayName,
                        exclude: false);
                    filter2 = XUnitFilter.CreateSingleFilter(
                        singleTestName: testDisplayName,
                        exclude: false);
                    collection = new XUnitFiltersCollection { filter, filter2};
                    testCase = new Mock<ITestCase>();
                    testCase.Setup(t => t.DisplayName).Returns(testDisplayName);

                    yield return new object[]
                    {
                        collection,
                        testCase.Object,
                        false,
                    };

                    // one filter that includes, other that excludes, should include
                    filter = XUnitFilter.CreateSingleFilter(
                        singleTestName: testDisplayName,
                        exclude: true);
                    filter2 = XUnitFilter.CreateSingleFilter(
                        singleTestName: testDisplayName,
                        exclude: false);
                    collection = new XUnitFiltersCollection { filter, filter2};
                    testCase = new Mock<ITestCase>();
                    testCase.Setup(t => t.DisplayName).Returns(testDisplayName);

                    yield return new object[]
                    {
                        collection,
                        testCase.Object,
                        false,
                    };
                }
            }

            public static IEnumerable<object[]> AssemblyFilters
            {
                get
                {
                    // single filter, exclude
                    var currentAssembly = Assembly.GetExecutingAssembly();
                    var assemblyName = $"{currentAssembly.GetName().Name}.dll";
                    var assemblyPath = currentAssembly.Location;
                    var assemblyInfo = new TestAssemblyInfo(currentAssembly, assemblyPath);
                    var filter = XUnitFilter.CreateAssemblyFilter(assemblyName: assemblyName!, exclude: true);
                    var collection = new XUnitFiltersCollection {filter};

                    yield return new object[]
                    {
                        collection,
                        assemblyInfo,
                        true,
                    };

                    // single filter, include
                    filter = XUnitFilter.CreateAssemblyFilter(assemblyName: assemblyName!, exclude: false);
                    collection = new XUnitFiltersCollection {filter};

                    yield return new object[]
                    {
                        collection,
                        assemblyInfo,
                        false,
                    };

                    // two excluding filters
                    filter = XUnitFilter.CreateAssemblyFilter(assemblyName: assemblyName!, exclude: true);
                    var filter2 = XUnitFilter.CreateAssemblyFilter(assemblyName: assemblyName!, exclude: true);
                    collection = new XUnitFiltersCollection {filter, filter2};

                    yield return new object[]
                    {
                        collection,
                        assemblyInfo,
                        true,
                    };

                    // two including filters
                    filter = XUnitFilter.CreateAssemblyFilter(assemblyName: assemblyName!, exclude: false);
                    filter2 = XUnitFilter.CreateAssemblyFilter(assemblyName: assemblyName!, exclude: false);
                    collection = new XUnitFiltersCollection {filter, filter2};

                    yield return new object[]
                    {
                        collection,
                        assemblyInfo,
                        false,
                    };

                    // one filter includes, other excludes
                    filter = XUnitFilter.CreateAssemblyFilter(assemblyName: assemblyName!, exclude: true);
                    filter2 = XUnitFilter.CreateAssemblyFilter(assemblyName: assemblyName!, exclude: false);
                    collection = new XUnitFiltersCollection {filter, filter2};

                    yield return new object[]
                    {
                        collection,
                        assemblyInfo,
                        false,
                    };
                }
            }

            [Theory]
            [MemberData(nameof (TestCaseFilters), MemberType = typeof (FiltersTestData))]
            void IsExcludedTestCase(XUnitFiltersCollection collection, ITestCase testCase, bool excluded)
            {
                var wasExcluded = collection.IsExcluded(testCase);
                Assert.Equal(excluded, wasExcluded);
            }

            [Theory]
            [MemberData(nameof (AssemblyFilters), MemberType = typeof (FiltersTestData))]
            void IsExcludedAsAssembly(XUnitFiltersCollection collection, TestAssemblyInfo assemblyInfo, bool excluded)
            {
                var wasExcluded = collection.IsExcluded(assemblyInfo);
                Assert.Equal(excluded, wasExcluded);
            }
        }

        [Fact]
        void AssemblyFilters()
        {
            var collection = new XUnitFiltersCollection();

            var assemblies = new [] {"MyFirstAssembly.dll", "SecondAssembly.dll", "ThirdAssembly.exe",};
            collection.AddRange(assemblies.Select(a => XUnitFilter.CreateAssemblyFilter(a, true)));

            var classes = new[] {"FirstClass", "SecondClass", "ThirdClass"};
            collection.AddRange(classes.Select(c => XUnitFilter.CreateClassFilter(c, true)));

            var methods = new[] {"FirstMethod", "SecondMethod"};
            collection.AddRange(methods.Select(m => XUnitFilter.CreateSingleFilter(m, true)));

            var namespaces = new[] {"Namespace"};
            collection.AddRange(namespaces.Select(n => XUnitFilter.CreateNamespaceFilter(n, true)));

            Assert.Equal(assemblies.Length, collection.AssemblyFilters.Count());
        }

        [Fact]
        void TestCaseFilters()
        {
            var collection = new XUnitFiltersCollection();
            var assemblies = new [] {"MyFirstAssembly.dll", "SecondAssembly.dll", "ThirdAssembly.exe",};
            collection.AddRange(assemblies.Select(a => XUnitFilter.CreateAssemblyFilter(a, true)));

            var classes = new[] {"FirstClass", "SecondClass", "ThirdClass"};
            collection.AddRange(classes.Select(c => XUnitFilter.CreateClassFilter(c, true)));

            var methods = new[] {"FirstMethod", "SecondMethod"};
            collection.AddRange(methods.Select(m => XUnitFilter.CreateSingleFilter(m, true)));

            var namespaces = new[] {"Namespace"};
            collection.AddRange(namespaces.Select(n => XUnitFilter.CreateNamespaceFilter(n, true)));

            Assert.Equal(collection.Count - assemblies.Length, collection.TestCaseFilters.Count());
        }
    }
}
