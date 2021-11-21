// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.DotNet.XHarness.TestRunners.Xunit
{
    public abstract class WasmApplicationEntryPoint
    {
        protected virtual string TestAssembly { get; set; } = "";
        protected virtual IEnumerable<string> ExcludedTraits { get; set; } = Array.Empty<string>();
        protected virtual IEnumerable<string> IncludedTraits { get; set; } = Array.Empty<string>();
        protected virtual IEnumerable<string> IncludedClasses { get; set; } = Array.Empty<string>();
        protected virtual IEnumerable<string> IncludedMethods { get; set; } = Array.Empty<string>();
        protected virtual IEnumerable<string> IncludedNamespaces { get; set; } = Array.Empty<string>();

        public async Task<int> Run()
        {
            var filters = new XunitFilters();

            foreach (var trait in ExcludedTraits) ParseEqualSeparatedArgument(filters.ExcludedTraits, trait);
            foreach (var trait in IncludedTraits) ParseEqualSeparatedArgument(filters.IncludedTraits, trait);
            foreach (var ns in IncludedNamespaces) filters.IncludedNamespaces.Add(ns);
            foreach (var cl in IncludedClasses) filters.IncludedClasses.Add(cl);
            foreach (var me in IncludedMethods) filters.IncludedMethods.Add(me);

            var result = await ThreadlessXunitTestRunner.Run(TestAssembly, printXml: true, filters, true);

            return result;
        }

        private static void ParseEqualSeparatedArgument(Dictionary<string, List<string>> targetDictionary, string argument)
        {
            var parts = argument.Split('=');
            if (parts.Length != 2 || string.IsNullOrEmpty(parts[0]) || string.IsNullOrEmpty(parts[1]))
            {
                throw new ArgumentException($"Invalid argument value '{argument}'.", nameof(argument));
            }

            var name = parts[0];
            var value = parts[1];
            List<string> excludedTraits;
            if (targetDictionary.TryGetValue(name, out excludedTraits!))
            {
                excludedTraits.Add(value);
            }
            else
            {
                targetDictionary[name] = new List<string> { value };
            }
        }
    }
}
