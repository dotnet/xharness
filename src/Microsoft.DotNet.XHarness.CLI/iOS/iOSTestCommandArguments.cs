// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.XHarness.CLI.Common;

namespace Microsoft.DotNet.XHarness.CLI.iOS
{
    internal class iOSTestCommandArguments : TestCommandArguments
    {
        public override IList<string> GetValidationErrors()
        {
            IList<string> errors = base.GetValidationErrors();

            if (Targets == null || Targets.Count == 0)
            {
                errors.Add($@"No targets specified. At least one target must be provided. " +
                    $"Available targets are:{Environment.NewLine}\t{string.Join(Environment.NewLine + "\t", GetAvailableTargets())}");
            }

            return errors;
        }

        private static IEnumerable<string> GetAvailableTargets() => new[]
        {
            "None",
            "Simulator_iOS",
            "Simulator_iOS32",
            "Simulator_iOS64",
            "Simulator_tvOS",
            "Simulator_watchOS",
            "Device_iOS",
            "Device_tvOS",
            "Device_watchOS",
        };
    }
}
