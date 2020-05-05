// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.CLI.CommandArguments;

namespace Microsoft.DotNet.XHarness.CLI.Commands
{
    internal abstract class TestCommand : XHarnessCommand
    {
        protected override XHarnessCommandArguments Arguments => TestArguments;
        protected abstract TestCommandArguments TestArguments { get; }

        public TestCommand() : base("test")
        {
        }
    }
}
