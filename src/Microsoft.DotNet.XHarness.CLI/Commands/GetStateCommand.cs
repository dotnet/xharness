// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.CLI.CommandArguments;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.Commands
{
    internal abstract class GetStateCommand : XHarnessCommand
    {
        protected override ICommandArguments Arguments => null;

        protected readonly OptionSet CommonOptions = new OptionSet();

        public GetStateCommand() : base("state")
        {
        }
    }
}
