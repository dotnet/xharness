﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Generic;

namespace Microsoft.DotNet.XHarness.TestRunners.Xunit
{
    public abstract class WasmApplicationEntryPoint
    {
        protected abstract string TestAssembly { get; set; }
        protected abstract IEnumerable<string> ExcludedTraits { get; set; }

        public int Run()
        {
            var testRunner = new ThreadlessXunitTestRunner();

            int result = testRunner.Run(TestAssembly, printXml: true, ExcludedTraits);

            return result;
        }
    }
}
