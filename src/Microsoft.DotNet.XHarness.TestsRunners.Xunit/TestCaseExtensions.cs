// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Xunit.Abstractions;
using Xunit.Sdk;
using Microsoft.DotNet.XUnitExtensions;
using NullMessageSink = Xunit.Sdk.NullMessageSink;

namespace Microsoft.DotNet.XHarness.TestsRunners.Xunit
{
    /// <summary>
    /// Useful extensions that make working with the ITestCase interface nicer within the runner.
    /// </summary>
    public static class TestCaseExtensions
    {
        static void DiscoverAllTraits(this ITestCase testCase)
        {
            if (testCase.Traits == null)
                return;

            // the following code is a work around an issue in the extensions that
            // was fixed in https://github.com/dotnet/arcade/pull/5425 but until we get the
            // arcade update, we do the following, check for the discoverer and then add
            // the expected category for the ActiveIssue
            var nullMessageSink = new NullMessageSink();
            var traitAttributes =  testCase.TestMethod?.Method?.GetCustomAttributes(typeof (ITraitAttribute));
            if (traitAttributes == null)
                return;
            foreach (var traitAttribute in traitAttributes)
            {
                var discovererAttribute = traitAttribute.GetCustomAttributes(typeof(TraitDiscovererAttribute)).FirstOrDefault();
                if (discovererAttribute == null)  continue;

                var discoverer = ExtensibilityPointFactory.GetTraitDiscoverer(nullMessageSink, discovererAttribute);

                if (discoverer == null || !(discoverer is ActiveIssueDiscoverer)) continue;

                // add the missing trait
                if (testCase.Traits.ContainsKey(XunitConstants.Category))
                {
                    if (!testCase.Traits[XunitConstants.Category].Contains("failing"))
                        testCase.Traits[XunitConstants.Category].Add("failing");
                }
                else
                {
                    testCase.Traits.Add(XunitConstants.Category, new List<string> {"failing"});
                }
            }
        }

        /// <summary>
        /// Returns boolean indicating whether the test case does have traits.
        /// </summary>
        /// <param name="testCase">The test case under test.</param>
        /// <returns>true if the test case has traits, false otherwise.</returns>
        public static bool HasTraits(this ITestCase testCase)
        {
            testCase.DiscoverAllTraits();
            return testCase.Traits != null && testCase.Traits.Count > 0;
        }

        public static bool TryGetTrait(this ITestCase testCase,
                                       string trait,
                                       [NotNullWhen(true)] out List<string>? values,
                                       StringComparison comparer = StringComparison.InvariantCultureIgnoreCase)
        {
            if (trait == null)
            {
                values = null;
                return false;
            }

            // there is no guarantee that the dict created by xunit is case insensitive, therefore, trygetvalue might
            // not return the value we are interested in. We have to loop, which is not ideal, but will be better
            // for our use case.
            foreach (var t in testCase.Traits.Keys)
            {
                if (trait.Equals(t, comparer))
                    return testCase.Traits.TryGetValue(t, out values);
            }

            values = null;
            return false;
        }

        /// <summary>
        /// Get the name of the test class that owns the test case.
        /// </summary>
        /// <param name="testCase">TestCase whose class we want to retrieve.</param>
        /// <returns>The name of the class that owns the test.</returns>
        public static string? GetTestClass(this ITestCase testCase) =>
            testCase.TestMethod?.TestClass?.Class?.Name?.Trim();

        public static string? GetNamespace(this ITestCase testCase)
        {
            var testClassName = testCase.GetTestClass();
            if (testClassName == null)
                return null;

            int dot = testClassName.LastIndexOf('.');
            return dot <= 0 ? null : testClassName.Substring(0, dot);
        }
    }
}
