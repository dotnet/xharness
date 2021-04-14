// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Android
{
    internal class AndroidRunCommandArguments : TestCommandArguments
    {
        private string? _packageName;

        /// <summary>
        /// If specified, attempt to run instrumentation with this name instead of the default for the supplied APK.
        /// If a given package has multiple instrumentations, failing to specify this may cause execution failure.
        /// </summary>
        public string? InstrumentationName { get; set; }

        public string PackageName
        {
            get => _packageName ?? throw new ArgumentNullException("Package name not specified");
            set => _packageName = value;
        }

        /// <summary>
        /// If specified, attempt to run APK on that device.
        /// If there is more than one device with required architecture, failing to specify this may cause execution failure.
        /// </summary>
        public string? DeviceId { get; set; }

        /// <summary>
        /// Folder to copy off for output of executing the specified APK
        /// </summary>
        public string? DeviceOutputFolder { get; set; }

        /// <summary>
        /// Passing these arguments as testing options to a test runner
        /// </summary>
        public Dictionary<string, string> InstrumentationArguments { get; } = new Dictionary<string, string>();

        /// <summary>
        /// Exit code returned by the instrumentation for a successful run. Defaults to 0.
        /// </summary>
        public int ExpectedExitCode { get; set; } = (int)Common.CLI.ExitCode.SUCCESS;

        /// <summary>
        /// Time to wait for boot completion. Defaults to 5 minutes.
        /// </summary>
        public TimeSpan LaunchTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Switch on/off wifi on the device.
        /// </summary>
        public WifiStatus Wifi { get; set; } = WifiStatus.Unknown;

        protected override OptionSet GetTestCommandOptions() => new()
        {
            { "device-out-folder=|dev-out=", "If specified, copy this folder recursively off the device to the path specified by the output directory",
                v => DeviceOutputFolder = RootPath(v)
            },
            { "instrumentation:|i:", "If specified, attempt to run instrumentation with this name instead of the default for the supplied APK.",
                v => InstrumentationName = v
            },
            { "expected-exit-code=", "If specified, sets the expected exit code for a successful instrumentation run.",
                v => {
                    if (int.TryParse(v, out var number))
                    {
                        ExpectedExitCode = number;
                        return;
                    }

                    throw new ArgumentException("expected-exit-code must be an integer");
                }
            },
            { "package-name=|p=", "Package name contained within the supplied APK",
                v => PackageName = v
            },
            {
                "device-id=", "Device where APK should be installed",
                v => DeviceId = v
            },
            {
                "launch-timeout=|lt=", "Time span in the form of \"00:00:00\" or number of seconds to wait for the device to boot to complete",
                v =>
                {
                    if (int.TryParse(v, out var timeout))
                    {
                        LaunchTimeout = TimeSpan.FromSeconds(timeout);
                        return;
                    }

                    if (TimeSpan.TryParse(v, out var timespan))
                    {
                        LaunchTimeout = timespan;
                        return;
                    }

                    throw new ArgumentException("launch-timeout must be an integer - a number of seconds, or a timespan (00:30:00)");
                }
            },
            { "wifi:", "Enable/disable wifi, is not checking by default. If passed without value, 'enable' is assumed.",
                v => Wifi = string.IsNullOrEmpty(v) ? WifiStatus.Enable : ParseArgument<WifiStatus>("wifi", v, invalidValues: WifiStatus.Unknown)
            },
            { "arg=", "Argument to pass to the instrumentation, in form key=value", v =>
                {
                    var argPair = v.Split('=');

                    if (argPair.Length != 2)
                    {
                        throw new ArgumentException($"The --arg argument expects 'key=value' format. Invalid format found in '{v}'");
                    }

                    if (InstrumentationArguments.ContainsKey(argPair[0]))
                    {
                        throw new ArgumentException($"Duplicate arg name '{argPair[0]}' found");
                    }

                    InstrumentationArguments.Add(argPair[0].Trim(), argPair[1].Trim());
                }
            },
        };

        public override void Validate()
        {
            base.Validate();

            // Validate this field
            _ = PackageName;
        }
    }
}
