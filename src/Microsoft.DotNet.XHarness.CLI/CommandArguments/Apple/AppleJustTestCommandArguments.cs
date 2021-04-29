// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple
{
    internal class AppleJustTestCommandArguments : AppleTestCommandArguments
    {
        private string? _bundleIdentifier = null;

        // We don't really use this one but we still validate it exists.
        // It is a hack around us re-using as much code as we can from AppleAppRunArguments.
        // This and the optionsToRemove below should get resolved with https://github.com/dotnet/xharness/issues/431
        public override string AppPackagePath { get; set; } = ".";

        /// <summary>
        /// Path to packaged app
        /// </summary>
        public string BundleIdentifier
        {
            get => _bundleIdentifier ?? throw new ArgumentException("You must provide ID of the app that will be uninstalled");
            set => _bundleIdentifier = value;
        }

        protected override OptionSet GetCommandOptions()
        {
            var options = base.GetCommandOptions();

            var newOptions = new OptionSet
            {
                {
                    "app|a=", "Bundle identifier of the app that should be uninstalled",
                    v => BundleIdentifier = v
                },
            };

            var optionsToRemove = new[]
            {
                "app|a=",
            };

            foreach (var option in options)
            {
                if (!optionsToRemove.Contains(option.Prototype))
                {
                    newOptions.Add(option);
                }
            }

            return newOptions;
        }

        public override void Validate()
        {
            base.Validate();
            BundleIdentifier = BundleIdentifier;
        }
    }
}
