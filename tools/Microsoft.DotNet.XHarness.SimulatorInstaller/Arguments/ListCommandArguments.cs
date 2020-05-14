﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Mono.Options;

namespace Microsoft.DotNet.XHarness.SimulatorInstaller.Arguments
{
    internal class ListCommandArguments : SimulatorInstallerCommandArguments
    {
        public bool ListInstalledOnly { get; private set; } = false;

        protected override OptionSet GetAdditionalOptions() => new OptionSet
        {
            { "installed", "Lists installed simulators only", v => ListInstalledOnly = true },
        };
    }
}
