// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Android
{
    internal class AndroidGetDeviceCommandArguments : TestCommandArguments
    {
        /// <summary>
        /// If specified, attempt to run on a compatible attached device, failing if unavailable.
        /// If not specified, we will open the apk using Zip APIs and guess what's usable based off folders found in under /lib
        /// </summary>
        public string? DeviceArchitecture { get; set; }

        protected override OptionSet GetTestCommandOptions() => new()
        {
            { "device-arch=", "If specified, forces running on a device with given architecture (x86, x86_64, arm64-v8a or armeabi-v7a). Otherwise inferred from supplied APK",
                v => DeviceArchitecture = v
            },
        };

        public override void Validate()
        {
            base.Validate();

            // Validate this field
            _ = AppPackagePath;
        }
    }
}
