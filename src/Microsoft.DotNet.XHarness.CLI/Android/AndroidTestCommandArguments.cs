// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.DotNet.XHarness.CLI.Common;

namespace Microsoft.DotNet.XHarness.CLI.Android
{
    internal class AndroidTestCommandArguments : TestCommandArguments
    {
        /// <summary>
        /// If specified, attempt to run instrumentation with this name instead of the default for the supplied APK.
        /// If a given package has multiple instrumentations, failing to specify this may cause execution failure.
        /// </summary>
        public string InstrumentationName { get; set; }

        /// <summary>
        /// If specified, attempt to run instrumentation with this name instead of the default for the supplied APK
        /// </summary>
        public string PackageName { get; set; }

        /// <summary>
        /// Folder to copy off for output of executing the specified APK
        /// </summary>
        public string DeviceOutputFolder { get; set; }

        public Dictionary<string, string> InstrumentationArguments { get; set; } = new Dictionary<string, string>();

        public override bool TryValidate([NotNullWhen(true)] out IEnumerable<string> errors)
        {
            bool baseResult = base.TryValidate(out var baseErrors);
            
            List<string> allErrors = new List<string>(baseErrors);

            if (string.IsNullOrEmpty(DeviceOutputFolder))
            {
                allErrors.Add("Must specify a value for device output folder");
            }
            errors = allErrors;

            return allErrors.Count == 0;
        }

        internal override IEnumerable<string> GetAvailableTargets()
        {
            return new[]
            {
                "TODO: To be filled in", // TODO
            };
        }
    }
}
