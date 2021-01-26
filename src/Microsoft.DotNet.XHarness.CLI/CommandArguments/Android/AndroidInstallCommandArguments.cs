// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Android
{
    internal class AndroidInstallCommandArguments : TestCommandArguments
    {
        private string? _packageName;

        public string PackageName
        {
            get => _packageName ?? throw new ArgumentException("Package name not specified");
            set => _packageName = value;
        }

        /// <summary>
        /// If specified, attempt to run on a compatible attached device, failing if unavailable.
        /// If not specified, we will open the apk using Zip APIs and guess what's usable based off folders found in under /lib
        /// </summary>
        public string? DeviceArchitecture { get; set; }

        protected override OptionSet GetTestCommandOptions() => new OptionSet
        {
            { "device-arch=", "If specified, forces running on a device with given architecture (x86, x86_64, or arm64_v8a). Otherwise inferred from supplied APK",
                v => DeviceArchitecture = v
            },
            { "package-name=|p=", "Package name contained within the supplied APK",
                v => PackageName = v
            },
        };

        public override void Validate()
        {
            // Validate this field
            PackageName = PackageName;
            AppPackagePath = AppPackagePath;
        }
    }
}
