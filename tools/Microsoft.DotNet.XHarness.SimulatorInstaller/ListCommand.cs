// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI;
using Microsoft.Extensions.Logging;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.SimulatorInstaller
{
    internal class ListCommandArguments : SimulatorInstallerCommandArguments
    {
        protected override OptionSet GetAdditionalOptions() => new OptionSet();
    }

    internal class ListCommand : SimulatorInstallerCommand
    {
        protected override string CommandUsage => Name;

        protected override string CommandDescription => "Lists installed simulators";

        private readonly ListCommandArguments _arguments = new ListCommandArguments();
        protected override SimulatorInstallerCommandArguments SimulatorInstallerArguments => _arguments;

        public ListCommand() : base("list", "Lists installed simulators")
        {
        }

        protected override Task<ExitCode> InvokeInternal(ILogger logger)
        {
            throw new NotImplementedException();
        }
    }
}
