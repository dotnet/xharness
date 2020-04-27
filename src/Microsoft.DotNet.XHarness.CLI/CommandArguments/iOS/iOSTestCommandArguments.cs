// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.iOS
{
    internal class iOSTestCommandArguments : TestCommandArguments
    {
        /// <summary>
        /// Path to where Xcode is located.
        /// </summary>
        public string XcodeRoot { get; set; }

        /// <summary>
        /// Path to the mlaunch binary.
        /// Default comes from the NuGet.
        /// </summary>
        public string MlaunchPath { get; set; } = Path.Join(
            Path.GetDirectoryName(System.Reflection.Assembly.GetAssembly(typeof(iOSTestCommandArguments)).Location),
            "..", "..", "..", "runtimes", "any", "native", "mlaunch", "bin", "mlaunch");

        /// <summary>
        /// Name of a specific device we want to target.
        /// </summary>
        public string DeviceName { get; set; }

        /// <summary>
        /// How long we wait before app starts and first test should start running.
        /// </summary>
        public TimeSpan LaunchTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Allows to specify the xml format to be used in the result files.
        /// </summary>
        public XmlResultJargon XmlResultJargon { get; set; } = XmlResultJargon.xUnit; // default by mono

        public IReadOnlyCollection<TestTarget> TestTargets { get; set; }

        public override IList<string> GetValidationErrors()
        {
            IList<string> errors = base.GetValidationErrors();

            if (Targets == null || Targets.Count == 0)
            {
                errors.Add($@"No targets specified. At least one target must be provided. " +
                    $"Available targets are:{Environment.NewLine}\t" +
                    $"{string.Join(Environment.NewLine + "\t", GetAvailableTargets())}");
            }
            else
            {
                var testTargets = new List<TestTarget>();

                foreach (string targetName in Targets)
                {
                    try
                    {
                        testTargets.Add(targetName.ParseAsAppRunnerTarget());
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        // let the user know that the target is not known
                        // and all the available ones.
                        var sb = new StringBuilder();
                        sb.AppendLine($"Failed to parse test target '{targetName}'. Available targets are:");
                        sb.AppendLine();
                        foreach (var val in Enum.GetValues(typeof(TestTarget)))
                        {
                            var enumString = ((TestTarget)val).AsString();
                            if (!string.IsNullOrEmpty(enumString))
                                sb.AppendLine($"\t* {enumString}");
                        }
                        errors.Add(sb.ToString());
                    }
                }

                TestTargets = testTargets;
            }

            if (!Path.IsPathRooted(MlaunchPath))
            {
                MlaunchPath = Path.Combine(Directory.GetCurrentDirectory(), MlaunchPath);
            }

            if (!File.Exists(MlaunchPath))
            {
                errors.Add($"Failed to find mlaunch at {MlaunchPath}");
            }

            if (XcodeRoot != null && !Path.IsPathRooted(XcodeRoot))
            {
                XcodeRoot = Path.Combine(Directory.GetCurrentDirectory(), XcodeRoot);
            }

            if (XcodeRoot != null && !Directory.Exists(XcodeRoot))
            {
                errors.Add($"Failed to find Xcode root at {XcodeRoot}");
            }

            if (XmlResultJargon == XmlResultJargon.Missing)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Failed to parse xml jargon. Available targets are:");
                sb.AppendLine();
                foreach (var val in new [] {XmlResultJargon.NUnitV2, XmlResultJargon.NUnitV3, XmlResultJargon.TouchUnit, XmlResultJargon.xUnit})
                {
                    sb.AppendLine($"\t* {val.ToString()}");
                }
                errors.Add(sb.ToString());
            }
            return errors;
        }

        private static IEnumerable<string> GetAvailableTargets() =>
            Enum.GetValues(typeof(TestTarget))
                .Cast<TestTarget>()
                .Select(t => t.AsString());
    }
}
