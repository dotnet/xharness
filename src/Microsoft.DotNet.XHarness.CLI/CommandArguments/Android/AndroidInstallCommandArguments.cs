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
        private string? _deviceId;

        public string PackageName
        {
            get => _packageName ?? throw new ArgumentNullException("Package name not specified");
            set => _packageName = value;
        }

        public string DeviceId
        {
            get => _deviceId ?? throw new ArgumentNullException("Device not specified");
            set => _deviceId = value;
        }

        protected override OptionSet GetTestCommandOptions() => new OptionSet
        {
            { "package-name=|p=", "Package name contained within the supplied APK",
                v => PackageName = v
            },
            {
                "device-id=", "Device where APK should be installed",
                v => DeviceId = v
            },
        };

        public override void Validate()
        {
            // Validate this field
            PackageName = PackageName;
            AppPackagePath = AppPackagePath;
            DeviceId = DeviceId;
        }
    }
}
