// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.Common.Execution;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple
{
    internal abstract class AppleCommandArguments : XHarnessCommandArguments
    {
        /// <summary>
        /// Path to where Xcode is located.
        /// </summary>
        public string? XcodeRoot { get; set; }

        /// <summary>
        /// Path to the mlaunch binary.
        /// Default comes from the NuGet.
        /// </summary>
        public string MlaunchPath { get; set; } = MacOSProcessManager.DetectMlaunchPath();

        protected override OptionSet GetCommandOptions() => new()
        {
            { "xcode=", "Path where Xcode is installed",
                v => XcodeRoot = RootPath(v)
            },
            { "mlaunch=", "Path to the mlaunch binary",
                v => MlaunchPath = RootPath(v)
            },
        };

        public override void Validate()
        {
            if (!File.Exists(MlaunchPath))
            {
                throw new ArgumentException(
                    $"Failed to find mlaunch at {MlaunchPath}. " +
                    $"Make sure you specify --mlaunch or set the {EnvironmentVariables.Names.MLAUNCH_PATH} env var. " +
                    $"See README.md for more information");
            }

            if (XcodeRoot != null && !Directory.Exists(XcodeRoot))
            {
                throw new ArgumentException($"Failed to find Xcode root at {XcodeRoot}");
            }
        }
    }
}
