// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.Common.CLI.Commands;

namespace Microsoft.DotNet.XHarness.CLI.Commands
{
    internal abstract class TestCommand : XHarnessCommand
    {
        public TestCommand(string? help, bool allowsExtraArgs = false) : base("test", allowsExtraArgs, help)
        {
        }
    }
}
