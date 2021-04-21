﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple
{
    internal class AppleGetStateCommandArguments : XHarnessCommandArguments
    {
        /// <summary>
        /// Path to the mlaunch binary.
        /// Default comes from the NuGet.
        /// </summary>
        public string MlaunchPath { get; set; } = Path.Join(
            Path.GetDirectoryName(System.Reflection.Assembly.GetAssembly(typeof(AppleTestCommandArguments))?.Location),
            "..", "..", "..", "runtimes", "any", "native", "mlaunch", "bin", "mlaunch");

        public bool ShowSimulatorsUUID { get; set; } = false;

        public bool ShowDevicesUUID { get; set; } = true;

        public bool UseJson { get; set; } = false;

        protected AppleGetStateCommandArguments()
        {
            string? pathFromEnv = Environment.GetEnvironmentVariable(EnvironmentVariables.Names.MLAUNCH_PATH);
            if (!string.IsNullOrEmpty(pathFromEnv))
            {
                MlaunchPath = pathFromEnv;
            }
            else
            {
                // This path is where mlaunch is when the .NET tool is extracted
                var assemblyPath = Path.GetDirectoryName(System.Reflection.Assembly.GetAssembly(typeof(AppleTestCommandArguments))?.Location);
                MlaunchPath = Path.Join(assemblyPath, "..", "..", "..", "runtimes", "any", "native", "mlaunch", "bin", "mlaunch");
            }
        }

        protected override OptionSet GetCommandOptions() => new()
        {
            { "mlaunch=", "Path to the mlaunch binary", v => MlaunchPath = RootPath(v) },
            { "include-simulator-uuid", "Include the simulators UUID. Defaults to false.", v => ShowSimulatorsUUID = v != null },
            { "include-devices-uuid", "Include the devices UUID.", v => ShowDevicesUUID = v != null },
            { "json", "Use json as the output format.", v => UseJson = v != null },
        };

        public override void Validate()
        {
            if (!File.Exists(MlaunchPath))
            {
                throw new ArgumentException($"Failed to find mlaunch at {MlaunchPath}");
            }
        }
    }
}
