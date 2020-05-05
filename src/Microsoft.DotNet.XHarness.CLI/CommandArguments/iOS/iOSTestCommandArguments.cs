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
    /// Specifies the channel that is used to comminicate with the device.
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

    internal class iOSTestCommandArguments : TestCommandArguments
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
        /// How long we wait before app starts and first test should start running.
        /// </summary>
        public TimeSpan LaunchTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Allows to specify the xml format to be used in the result files.
        /// </summary>
        public XmlResultJargon XmlResultJargon { get; set; } = XmlResultJargon.xUnit; // default by mono

        /// <summary>
        /// The way the simulator/device talks back to XHarness.
        /// </summary>
        public CommunicationChannel CommunicationChannel { get; set; } = CommunicationChannel.UsbTunnel;

        public override IReadOnlyCollection<string> Targets
        {
            get => TestTargets.Select(t => t.AsString()).ToArray();
            protected set
            {
                var testTargets = new List<TestTarget>();

                foreach (string targetName in value ?? throw new ArgumentNullException("Targets cannot be empty"))
                {
                    try
                    {
                        testTargets.Add(targetName.ParseAsAppRunnerTarget());
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        throw new ArgumentException(
                            $"Failed to parse test target '{targetName}'. Available targets are:" +
                            GetAllowedValues(t => t.AsString(), invalidValues: TestTarget.None));
                    }
                }

                TestTargets = testTargets;
            }
        }

        /// <summary>
        /// Parsed strong-typed targets.
        /// </summary>
        public IReadOnlyCollection<TestTarget> TestTargets { get; private set; } = Array.Empty<TestTarget>();

        protected override OptionSet GetTestCommandOptions() => new OptionSet
        {
            { "xcode=", "Path where Xcode is installed",
                v => XcodeRoot = RootPath(v)
            },
            { "mlaunch=", "Path to the mlaunch binary",
                v => MlaunchPath = RootPath(v)
            },
            { "device-name=", "Name of a specific device, if you wish to target one",
                v => DeviceName = v
            },
            { "communication-channel=", $"The communication channel to use to communicate with the default. Can be {CommunicationChannel.Network} and {CommunicationChannel.UsbTunnel}. Default is {CommunicationChannel.UsbTunnel}",
                v => CommunicationChannel = ParseArgument<CommunicationChannel>("communication-channel", v)
            },
            { "launch-timeout=|lt=", "Time span, in seconds, to wait for the iOS app to start.",
                v =>
                {
                    if (!int.TryParse(v, out var launchTimeout))
                    {
                        throw new ArgumentException("launch-timeout must be an integer - a number of seconds");
                    }

                    LaunchTimeout = TimeSpan.FromSeconds(launchTimeout);
                }
            },
            { "xml-jargon=|xj=", $"The xml format to be used in the unit test results. Can be {XmlResultJargon.TouchUnit}, {XmlResultJargon.NUnitV2}, {XmlResultJargon.NUnitV3} or {XmlResultJargon.xUnit}.",
                v => XmlResultJargon = ParseArgument("xml-jargon", v, invalidValues: XmlResultJargon.Missing)
            },
        };

        public override void Validate()
        {
            base.Validate();

            if (!Directory.Exists(AppPackagePath))
            {
                throw new ArgumentException($"Failed to find the app bundle at {AppPackagePath}");
            }

            if (TestTargets.Count == 0)
            {
                throw new ArgumentException(
                    "No targets specified. At least one target must be provided. Available targets are:" +
                    GetAllowedValues(t => t.AsString(), invalidValues: TestTarget.None));
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
