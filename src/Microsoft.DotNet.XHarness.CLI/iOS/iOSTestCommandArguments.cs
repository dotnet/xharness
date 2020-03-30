// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.CLI.Common;
using System.Collections.Generic;

namespace Microsoft.DotNet.XHarness.CLI.iOS
{

    internal class iOSTestCommandArguments : TestCommandArguments
    {
        internal override IEnumerable<string> GetAvailableTargets()
        {
            return new[]
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
}
