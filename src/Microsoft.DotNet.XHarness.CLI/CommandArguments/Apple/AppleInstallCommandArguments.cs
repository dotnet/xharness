// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple
{
    internal class AppleInstallCommandArguments : AppleAppRunArguments
    {
        protected override OptionSet GetCommandOptions()
        {
            OptionSet options = base.GetCommandOptions();

            var optionsToRemove = new[]
            {
                "enable-lldb",
                "set-env=",
            };

            var installOptions = new OptionSet();
            foreach (var option in options)
            {
                // Replaced by ours
                if (!optionsToRemove.Contains(option.Prototype))
                {
                    installOptions.Add(option);
                }
            }

            return installOptions;
        }
    }
}
