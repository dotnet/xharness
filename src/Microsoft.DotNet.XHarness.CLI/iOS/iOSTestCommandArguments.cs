// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.XHarness.CLI.Common;
using Microsoft.DotNet.XHarness.iOS.Shared;

namespace Microsoft.DotNet.XHarness.CLI.iOS
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
        public string MlaunchPath { get; set; } = Path.Join("..", "..", "..", "runtimes", "any", "native", "mlaunch", "bin", "mlaunch");

        /// <summary>
        /// How long we wait before app starts and first test should start running.
        /// </summary>
        public TimeSpan LaunchTimeout { get; set; } = TimeSpan.FromMinutes(5);

        public IReadOnlyCollection<TestTarget> TestTargets { get; set; }

        public override IList<string> GetValidationErrors()
        {
            IList<string> errors = base.GetValidationErrors();

            if (Targets == null || Targets.Count == 0)
            {
                errors.Add($@"No targets specified. At least one target must be provided. " +
                    $"Available targets are:{Environment.NewLine}\t{string.Join(Environment.NewLine + "\t", TestTargetExtensions.TestTargetNames.Keys)}");
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

            var testTargets = new List<TestTarget>();

            foreach (string targetName in Targets)
            {
                if (TestTargetExtensions.TestTargetNames.TryGetValue(targetName, out TestTarget target))
                {
                    testTargets.Add(target);
                }
                else
                {
                    errors.Add($"Failed to parse test target '{targetName}'");
                }
            }

            TestTargets = testTargets;

            return errors;
        }
    }
}
