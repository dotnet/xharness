// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.SimulatorInstaller.Arguments
{
    internal class InstallCommandArguments : SimulatorInstallerCommandArguments
    {
        public IEnumerable<string> Simulators { get; } = new List<string>();
        public bool Force { get; private set; } = false;

        protected override OptionSet GetAdditionalOptions() => new OptionSet
        {
            { "s|simulator=", "ID of the Simulator to install. Repeat multiple times to define more", v => ((IList<string>)Simulators).Add(v) },
            { "force", "Install again even if already installed", v => Force = true },
        };

        public override void Validate()
        {
            base.Validate();

            if (!Simulators.Any())
            {
                throw new ArgumentException("At least one --simulator is expected");
            }
        }
    }
}
