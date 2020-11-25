// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.XHarness.Common;
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

    internal class iOSTestCommandArguments : iOSAppRunArguments
    {
        private readonly List<string> _singleMethodFilters = new List<string>();
        private readonly List<string> _classMethodFilters = new List<string>();

        /// <summary>
        /// Methods to be included in the test run while all others are ignored.
        /// </summary>
        public IEnumerable<string> SingleMethodFilters => _singleMethodFilters;

        /// <summary>
        /// Tests classes to be included in the run while all others are ignored.
        /// </summary>
        public IEnumerable<string> ClassMethodFilters => _classMethodFilters;

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

        protected override OptionSet GetCommandOptions()
        {
            var options = base.GetCommandOptions();

            var testOptions = new OptionSet
            {
                {
                    "launch-timeout=|lt=", "TimeSpan or number of seconds to wait for the iOS app to start",
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
                {
                    "xml-jargon=|xj=", $"The xml format to be used in the unit test results. Can be {XmlResultJargon.TouchUnit}, {XmlResultJargon.NUnitV2}, {XmlResultJargon.NUnitV3} or {XmlResultJargon.xUnit}.",
                    v => XmlResultJargon = ParseArgument("xml-jargon", v, invalidValues: XmlResultJargon.Missing)
                },
                {
                    "communication-channel=", $"The communication channel to use to communicate with the default. Can be {CommunicationChannel.Network} and {CommunicationChannel.UsbTunnel}. Default is {CommunicationChannel.UsbTunnel}",
                    v => CommunicationChannel = ParseArgument<CommunicationChannel>("communication-channel", v)
                },
                {
                    "method|m=", "Method to be ran in the test application. When this parameter is used only the " +
                    "tests that have been provided by the '--method' and '--class' arguments will be ran. All other test will be " +
                    "ignored. Can be used more than once.",
                    v => _singleMethodFilters.Add(v)
                },
                {
                    "class|c=", "Test class to be ran in the test application. When this parameter is used only the " +
                    "tests that have been provided by the '--method' and '--class' arguments will be ran. All other test will be " +
                    "ignored. Can be used more than once.",
                    v => _classMethodFilters.Add(v)
                },
            };

            foreach (var option in testOptions)
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
