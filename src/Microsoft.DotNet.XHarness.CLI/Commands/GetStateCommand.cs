// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.Commands
{
    internal abstract class GetStateCommand : XHarnessCommand
    {
        protected abstract string BaseCommand { get; }

        protected OptionSet CommonOptions { get; }

        public GetStateCommand() : base("state")
        {
            CommonOptions = new OptionSet
            {
                $"usage: {BaseCommand} state",
                "",
                "Print information about the current machine, such as host machine info and device status",
            };
        }
    }
}
