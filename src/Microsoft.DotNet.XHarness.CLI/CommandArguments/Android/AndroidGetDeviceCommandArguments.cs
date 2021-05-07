// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Android
{
    internal class AndroidGetDeviceCommandArguments : XHarnessCommandArguments
    {
        private string? _appPackagePath = null;
        private readonly List<string> _deviceArchitecture = new();

        /// <summary>
        /// Path to packaged app
        /// </summary>
        public string AppPackagePath
        {
            get => _appPackagePath ?? throw new ArgumentException("You must provide a path for the app that will be tested.");
            set => _appPackagePath = value;
        }

        /// <summary>
        /// If specified, attempt to run on a compatible attached device, failing if unavailable.
        /// If not specified, we will open the apk using Zip APIs and guess what's usable based off folders found in under /lib
        /// </summary>
        public IEnumerable<string> DeviceArchitecture => _deviceArchitecture;

        protected override OptionSet GetCommandOptions() => new()
        {
            { "app|a=", "Path to already-packaged app",
                v => AppPackagePath = RootPath(v)
            },
            {
                "device-arch=",
                "If specified, forces running on a device with given architecture (x86, x86_64, arm64-v8a or armeabi-v7a). Otherwise inferred from supplied APK. " +
                "Can be used more than once.",
                v => _deviceArchitecture.Add(v)
            },
        };

        public override void Validate()
        {

            foreach (var archName in _deviceArchitecture ?? throw new ArgumentException("architecture cannot be empty"))
            {
                try
                {
                    AndroidArchitectureHelper.ParseAsAndroidArchitecture(archName);
                }
                catch (ArgumentOutOfRangeException)
                {
                    throw new ArgumentException(
                        $"Failed to parse architecture '{archName}'. Available architectures are:" +
                        GetAllowedValues<AndroidArchitecture>(t => t.AsString()));
                }
            }
            // Validate this field
            _ = AppPackagePath;
        }
    }
}
