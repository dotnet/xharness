// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.XHarness.TestRunners.Common;
using Xunit.Abstractions;

namespace Microsoft.DotNet.XHarness.TestRunners.Xunit
{
    /// <summary>
    /// Class that contains a collection of filters and can be used to decide if a test should be executed or not.
    /// </summary>
    internal class XUnitFiltersCollection : List<XUnitFilter>
    {

        /// <summary>
        /// Gets/sets if by default all tests are ran.
        /// </summary>
        public bool RunAllTestsByDefault { get; set; } = true;

        /// <summary>
        /// Return all the filters that are applied to assemblies.
        /// </summary>
        public IEnumerable<XUnitFilter> AssemblyFilters
            => Enumerable.Where (this, f => f.FilterType == XUnitFilterType.Assembly);

        /// <summary>
        /// Return all the filters that are applied to test cases.
        /// </summary>
        public IEnumerable<XUnitFilter> TestCaseFilters
            => Enumerable.Where(this, f => f.FilterType != XUnitFilterType.Assembly);

        // loop over all the filters, if we have conflicting filters, that is, one exclude and other one
        // includes, we will always include since it is better to run a test thant to skip it and think
        // you ran in.
        private bool IsExcludedInternal(IEnumerable<XUnitFilter> filters, Func<XUnitFilter, bool> isExcludedCb)
        {
            var isExcluded = !RunAllTestsByDefault;
            foreach (var filter in filters)
            {
                var doesExclude = isExcludedCb(filter);
                if (filter.Exclude)
                {
                    isExcluded |= doesExclude;
                }
                else
                {
                    // filter does not exclude, that means that if it include, we should include and break the
                    // loop, always include
                    if (!doesExclude) return false;
                }
            }

            return isExcluded;
        }

        public bool IsExcluded(TestAssemblyInfo assembly, Action<string>? log = null) =>
            IsExcludedInternal(AssemblyFilters, f => f.IsExcluded(assembly, log));

        public bool IsExcluded(ITestCase testCase, Action<string>? log = null) =>
            IsExcludedInternal(TestCaseFilters, f => f.IsExcluded(testCase, log));
    }
}
