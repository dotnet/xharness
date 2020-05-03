// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.iOS
{
    internal class iOSGetStateCommandArguments : XHarnessCommandArguments
    {
        /// <summary>
        /// Path to the mlaunch binary.
        /// Default comes from the NuGet.
        /// </summary>
        public string MlaunchPath { get; set; } = Path.Join(
            Path.GetDirectoryName(System.Reflection.Assembly.GetAssembly(typeof(iOSTestCommandArguments))?.Location),
            "..", "..", "..", "runtimes", "any", "native", "mlaunch", "bin", "mlaunch");

        public bool ShowSimulatorsUUID { get; set; } = false;

        public bool ShowDevicesUUID { get; set; } = true;

        public bool UseJson { get; set; } = false;

        public override OptionSet GetOptions()
        {
            var options = new OptionSet
            {
                $"usage: ios state [OPTIONS]",
                "",
                "Print information about the current machine, such as host machine info and device status",

                { "mlaunch=", "Path to the mlaunch binary", v => MlaunchPath = RootPath(v) },
                { "include-simulator-uuid", "Include the simulators UUID. Defaults to false.", v => ShowSimulatorsUUID = v != null },
                { "include-devices-uuid", "Include the devices UUID.", v => ShowDevicesUUID = v != null },
                { "json", "Use json as the output format.", v => UseJson = v != null },
            };

            foreach (var option in base.GetOptions())
            {
                options.Add(option);
            }

            return options;
        }

        public override void Validate()
        {
            if (!File.Exists(MlaunchPath))
            {
                throw new ArgumentException($"Failed to find mlaunch at {MlaunchPath}");
            }
        }
    }
}
