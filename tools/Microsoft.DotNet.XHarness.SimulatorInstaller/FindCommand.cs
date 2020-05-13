// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI;
using Microsoft.Extensions.Logging;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.SimulatorInstaller
{
    internal class FindCommandArguments : SimulatorInstallerCommandArguments
    {
        public IEnumerable<string> Simulators { get; } = new List<string>();

        protected override OptionSet GetAdditionalOptions() => new OptionSet
        {
            { "s|simulator=", "ID of the Simulator to look for. Repeat multiple times to define more", v => ((IList<string>)Simulators).Add(v) },
        };

        public override void Validate()
        {
            if (!Simulators.Any())
            {
                throw new ArgumentException("At least one --simulator is expected");
            }
        }
    }

    internal class FindCommand : SimulatorInstallerCommand
    {
        protected override string CommandUsage => "find [OPTIONS]";

        protected override string CommandDescription => "Finds whether given simulators are installed";

        private readonly FindCommandArguments _arguments = new FindCommandArguments();
        protected override SimulatorInstallerCommandArguments SimulatorInstallerArguments => _arguments;

        public FindCommand() : base("find", "Finds whether given simulators are installed")
        {
        }

        protected override Task<ExitCode> InvokeInternal(ILogger logger)
        {
            throw new NotImplementedException();
        }
    }
}
