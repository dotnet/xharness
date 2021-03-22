// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple.Simulators
{
    internal class InstallCommandArguments : SimulatorsCommandArguments
    {
        private readonly List<string> _simulators = new List<string>();
        public IEnumerable<string> Simulators => _simulators;
        public bool Force { get; private set; } = false;

        protected override OptionSet GetAdditionalOptions() => new OptionSet
        {
            { "s|simulator=",
                "ID of the Simulator to install (e.g. com.apple.pkg.AppleTVSimulatorSDK14_2). " +
                "You can also use the format in which you specify apple targets for XHarness tests. " +
                "Repeat multiple times to define more",
                v =>
                {
                    if (v.StartsWith("com.apple.pkg."))
                    {
                        _simulators.Add(v);
                        return;
                    }

                    TestTargetOs target;
                    try
                    {
                        target = v.ParseAsAppRunnerTargetOs();
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        throw new ArgumentException(
                            $"Failed to parse simulator '{v}'. Available values are ios-simulator, tvos-simulator and watchos-simulator." +
                            Environment.NewLine + Environment.NewLine +
                            "You need to also specify the version. Example: ios-simulator_13.4");
                    }

                    if (string.IsNullOrEmpty(target.OSVersion))
                    {
                        throw new ArgumentException($"Failed to parse simulator '{v}'. " +
                            $"You need to specify the exact version. Example: ios-simulator_13.4");
                    }

                    string simulatorName = target.Platform switch
                    {
                        TestTarget.Simulator_iOS => "iPhone",
                        TestTarget.Simulator_tvOS => "AppleTV",
                        TestTarget.Simulator_watchOS => "Watch",
                        _ => throw new ArgumentException($"Failed to parse simulator '{v}'. " +
                            "Available values are ios-simulator, tvos-simulator and watchos-simulator." +
                            Environment.NewLine + Environment.NewLine +
                            "You need to also specify the version. Example: ios-simulator_13.4"),
                    };

                    // e.g. com.apple.pkg.AppleTVSimulatorSDK14_3
                    _simulators.Add($"com.apple.pkg.{simulatorName}SimulatorSDK{target.OSVersion.Replace(".", "_")}");
                }
            },
            { "force", "Install again even if already installed", v => Force = true },
        };

        public override void Validate()
        {
            base.Validate();

            if (!Simulators.Any())
            {
                throw new ArgumentException("At least one --simulator is expected");
            }
        }
    }
}
