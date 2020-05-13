// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI;
using Microsoft.DotNet.XHarness.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.CLI.Commands;
using Microsoft.Extensions.Logging;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.SimulatorInstaller
{
    internal class ListCommandArguments : XHarnessCommandArguments
    {
        protected override OptionSet GetCommandOptions() => new OptionSet();

        public override void Validate()
        {
        }
    }

    internal class ListCommand : XHarnessCommand
    {
        protected override string CommandUsage => Name;

        protected override string CommandDescription => "Lists installed simulators";

        private readonly ListCommandArguments _arguments = new ListCommandArguments();
        protected override XHarnessCommandArguments Arguments => _arguments;

        public ListCommand() : base("list")
        {
        }

        protected override Task<ExitCode> InvokeInternal(ILogger logger)
        {
            throw new NotImplementedException();
        }
    }
}
