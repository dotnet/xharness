// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Android
{
    internal class AndroidUninstallCommandArguments : TestCommandArguments
    {
        private string? _packageName;

        public string PackageName
        {
            get => _packageName ?? throw new ArgumentException("Package name not specified");
            set => _packageName = value;
        }

        protected override OptionSet GetTestCommandOptions() => new OptionSet
        {
            { "package-name=|p=", "Package name contained within the supplied APK",
                v => PackageName = v
            },
        };

        public override void Validate()
        {
            // Validate this field
            PackageName = PackageName;
        }
    }
}
