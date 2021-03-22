// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple.Simulators
{
    internal class InstallCommandArguments : SimulatorsCommandArguments
    {
        public bool Force { get; private set; } = false;

        protected override OptionSet GetAdditionalOptions() => new OptionSet
        {
            { "force", "Install again even if already installed", v => Force = true },
        };
    }
}
