// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments;

/// <summary>
/// Shared --enable-coverage switch used across all platforms (Android, Apple, WASM).
/// </summary>
internal class EnableCoverageArgument : SwitchArgument
{
    public EnableCoverageArgument() : base("enable-coverage", "Enable code coverage collection during test execution", false)
    {
    }
}
