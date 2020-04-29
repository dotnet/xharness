// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.iOS
{
    internal class iOSGetStateCommandArguments : XHarnessCommandArguments
    {
        /// <summary>
        /// Path to the mlaunch binary.
        /// Default comes from the NuGet.
        /// </summary>
        public string MlaunchPath { get; set; } = Path.Join(
            Path.GetDirectoryName(System.Reflection.Assembly.GetAssembly(typeof(iOSTestCommandArguments)).Location),
            "..", "..", "..", "runtimes", "any", "native", "mlaunch", "bin", "mlaunch");

        public bool ShowSimulatorsUUID { get; set; } = false;

        public bool ShowDevicesUUID { get; set; } = true;

        public bool UseJson { get; set; } = false;

        public override IList<string> GetValidationErrors()
        {
            var errors = new List<string>();

            MlaunchPath = RootPath(MlaunchPath);
            if (!File.Exists(MlaunchPath))
            {
                errors.Add($"Failed to find mlaunch at {MlaunchPath}");
            }

            return errors;
        }
    }
}
