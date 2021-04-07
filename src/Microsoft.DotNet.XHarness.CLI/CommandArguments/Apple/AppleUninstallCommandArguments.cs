// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple
{
    internal class AppleUninstallCommandArguments : AppRunCommandArguments
    {
        private string? _bundleIdentifier = null;

        /// <summary>
        /// Path to packaged app
        /// </summary>
        public string BundleIdentifier
        {
            get => _bundleIdentifier ?? throw new ArgumentException("You must provide ID of the app that will be uninstalled");
            set => _bundleIdentifier = value;
        }

        public AppleUninstallCommandArguments()
        {
            // We are validating that it exists, so we just set the current one and don't use it later
            // It is replaced with the bundle identifier argument
            AppPackagePath = ".";
        }

        protected override OptionSet GetCommandOptions()
        {
            var options = base.GetCommandOptions();

            var runOptions = new OptionSet
            {
                {
                    "app|a=", "Bundle identifier of the app that should be uninstalled",
                    v => AppPackagePath = RootPath(v)
                },
            };

            foreach (var option in runOptions)
            {
                // Replaced by ours
                if (option.Prototype == "app|a")
                {
                    continue;
                }

                options.Add(option);
            }

            return options;
        }
    }
}
