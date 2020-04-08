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
        /// If specified, attempt to run instrumentation with this name instead of the default for the supplied APK
        /// </summary>
        public string InstrumentationName { get; set; }

        public Dictionary<string, string> InstrumentationArguments { get; set; } = new Dictionary<string, string>();

        public override bool TryValidate([NotNullWhen(true)] out IEnumerable<string> errors)
        {
            if (!base.TryValidate(out errors))
            {
                return false;
            }

            // TODO: Android specific validation checks
            return true;
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
