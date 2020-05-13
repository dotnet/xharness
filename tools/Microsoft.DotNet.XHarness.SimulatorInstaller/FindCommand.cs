﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI;
using Microsoft.DotNet.XHarness.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.CLI.Commands;
using Microsoft.Extensions.Logging;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.SimulatorInstaller
{
    internal class FindCommandArguments : XHarnessCommandArguments
    {
        public IEnumerable<string> Simulators { get; } = new List<string>();

        protected override OptionSet GetCommandOptions() => new OptionSet
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

    internal class FindCommand : XHarnessCommand
    {
        protected override string CommandUsage => "find [OPTIONS]";

        protected override string CommandDescription => "Finds whether given simulators are installed";

        private readonly FindCommandArguments _arguments = new FindCommandArguments();
        protected override XHarnessCommandArguments Arguments => _arguments;

        public FindCommand() : base("find")
        {
        }

        protected override Task<ExitCode> InvokeInternal(ILogger logger)
        {
            throw new NotImplementedException();
        }
    }
}
