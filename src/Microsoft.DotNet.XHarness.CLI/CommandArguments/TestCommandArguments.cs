// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments
{
    internal abstract class TestCommandArguments : AppRunCommandArguments
    {
        private readonly List<string> _singleMethodFilters = new();
        private readonly List<string> _classMethodFilters = new();

        /// <summary>
        /// Methods to be included in the test run while all others are ignored.
        /// </summary>
        public IEnumerable<string> SingleMethodFilters => _singleMethodFilters;

        /// <summary>
        /// Tests classes to be included in the run while all others are ignored.
        /// </summary>
        public IEnumerable<string> ClassMethodFilters => _classMethodFilters;

        protected override OptionSet GetCommandOptions()
        {
            var options = base.GetCommandOptions();

            var testOptions = new OptionSet
            {
                {
                    "method|m=", "Method to be ran in the test application. When this parameter is used only the " +
                    "tests that have been provided by the '--method' and '--class' arguments will be ran. All other test will be " +
                    "ignored. Can be used more than once.",
                    v => _singleMethodFilters.Add(v)
                },
                {
                    "class|c=", "Test class to be ran in the test application. When this parameter is used only the " +
                    "tests that have been provided by the '--method' and '--class' arguments will be ran. All other test will be " +
                    "ignored. Can be used more than once.",
                    v => _classMethodFilters.Add(v)
                },
            };

            foreach (var option in testOptions)
            {
                options.Add(option);
            }

            foreach (var option in GetTestCommandOptions())
            {
                options.Add(option);
            }

            return options;
        }

        protected abstract OptionSet GetTestCommandOptions();
    }
}
