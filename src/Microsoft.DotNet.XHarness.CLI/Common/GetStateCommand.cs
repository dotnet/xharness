// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.Common
{
    internal abstract class GetStateCommand : XHarnessCommand
    {
        protected override ICommandArguments Arguments => null;


        protected readonly OptionSet CommonOptions;

        public GetStateCommand() : base("state")
        {
            CommonOptions = new OptionSet { };
        }
    }
}
