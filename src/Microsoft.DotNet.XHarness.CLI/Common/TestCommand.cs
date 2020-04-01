// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.Common
{
    internal abstract class TestCommand : XHarnessCommand
    {
        protected override ICommandArguments Arguments => TestArguments;
        protected abstract ITestCommandArguments TestArguments { get; }

        public TestCommand() : base("test")
        {
        }
    }
}
