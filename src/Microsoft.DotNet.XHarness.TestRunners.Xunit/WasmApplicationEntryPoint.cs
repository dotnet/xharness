// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.TestRunners.Common;

namespace Microsoft.DotNet.XHarness.TestRunners.Xunit
{
    public abstract class WasmApplicationEntryPoint
    {
        protected abstract string TestAssembly { get; set; }
        protected abstract IEnumerable<string> ExcludedTraits { get; set; }

        public int Run()
        {
            var testRunner = new ThreadlessXunitTestRunner();

            var result = testRunner.Run(TestAssembly, printXml: false, ExcludedTraits);

            return result;
        }
    }
}
