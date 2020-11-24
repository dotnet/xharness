// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.iOS
{
    /// <summary>
    /// Specifies the channel that is used to communicate with the device.
    /// </summary>
    internal enum CommunicationChannel
    {
        /// <summary>
        /// Connect to the device using the LAN or WAN.
        /// </summary>
        Network,
        /// <summary>
        /// Connect to the device using a tcp-tunnel
        /// </summary>
        UsbTunnel,
    }

    internal class iOSRunCommandArguments : AppRunCommandArguments
    {
        /// <summary>
        /// Path to where Xcode is located.
        /// </summary>
        public string? XcodeRoot { get; set; }

        /// <summary>
        /// Path to the mlaunch binary.
        /// Default comes from the NuGet.
        /// </summary>
        public string MlaunchPath { get; set; } = Path.Join(
            Path.GetDirectoryName(System.Reflection.Assembly.GetAssembly(typeof(iOSTestCommandArguments))?.Location),
            "..", "..", "..", "runtimes", "any", "native", "mlaunch", "bin", "mlaunch");

        /// <summary>
        /// Name of a specific device we want to target.
        /// </summary>
        public string? DeviceName { get; set; }

        /// <summary>
        /// Enable the lldb debugger to be used with the launched application.
        /// </summary>
        public bool EnableLldb { get; set; }

        public override IReadOnlyCollection<string> Targets
        {
            get => RunTargets.Select(t => t.AsString()).ToArray();
            protected set
            {
                var testTargets = new List<TestTargetOs>();

                foreach (string targetName in value ?? throw new ArgumentNullException("Targets cannot be empty"))
                {
                    try
                    {
                        testTargets.Add(targetName.ParseAsAppRunnerTargetOs());
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        throw new ArgumentException(
                            $"Failed to parse test target '{targetName}'. Available targets are:" +
                            GetAllowedValues(t => t.AsString(), invalidValues: TestTarget.None) +
                            Environment.NewLine + Environment.NewLine +
                            "You can also specify desired iOS/tvOS/WatchOS version. Example: ios-simulator-64_13.4");
                    }
                }

                RunTargets = testTargets;
            }
        }

        /// <summary>
        /// Parsed strong-typed targets.
        /// </summary>
        public IReadOnlyCollection<TestTargetOs> RunTargets { get; private set; } = Array.Empty<TestTargetOs>();

        protected override OptionSet GetCommandOptions()
        {
            var options = base.GetCommandOptions();

            var runOptions = new OptionSet
            {
                {
                    "xcode=", "Path where Xcode is installed",
                    v => XcodeRoot = RootPath(v)
                },
                {
                    "mlaunch=", "Path to the mlaunch binary",
                    v => MlaunchPath = RootPath(v)
                },
                {
                    "device-name=", "Name of a specific device, if you wish to target one",
                    v => DeviceName = v
                },
                {
                    "enable-lldb", "Allow to debug the launched application using lldb.",
                    v => EnableLldb = v != null
                },
            };

            foreach (var option in runOptions)
            {
                options.Add(option);
            }

            return options;
        }

        public override void Validate()
        {
            base.Validate();

            if (!Directory.Exists(AppPackagePath))
            {
                throw new ArgumentException($"Failed to find the app bundle at {AppPackagePath}");
            }

            if (RunTargets.Count == 0)
            {
                throw new ArgumentException(
                    "No targets specified. At least one target must be provided. Available targets are:" +
                    GetAllowedValues(t => t.AsString(), invalidValues: TestTarget.None) +
                    Environment.NewLine + Environment.NewLine +
                    "You can also specify desired iOS/tvOS/WatchOS version. Example: ios-simulator-64_13.4");
            }

            if (!File.Exists(MlaunchPath))
            {
                throw new ArgumentException($"Failed to find mlaunch at {MlaunchPath}");
            }

            if (XcodeRoot != null && !Directory.Exists(XcodeRoot))
            {
                throw new ArgumentException($"Failed to find Xcode root at {XcodeRoot}");
            }
        }
    }
}
