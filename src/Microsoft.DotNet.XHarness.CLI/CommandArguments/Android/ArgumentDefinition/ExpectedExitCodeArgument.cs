// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Android
{
    /// <summary>
    /// Exit code returned by the instrumentation for a successful run. Defaults to 0.
    /// </summary>
    internal class ExpectedExitCodeArgument : IntArgument
    {
        public ExpectedExitCodeArgument(int defaultValue)
            : base("expected-exit-code=", "If specified, sets the expected exit code for a successful instrumentation run", defaultValue)
        {
        }
    }
}
