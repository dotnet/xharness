// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.CLI.Commands.Apple.Simulators;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Apple
{
    public class AppleCommandSet : CommandSet
    {
        public AppleCommandSet() : base("apple")
        {
            // Commands for full install/execute/uninstall flows
            Add(new AppleTestCommand());
            Add(new AppleRunCommand());

            // Commands for more fine grained control over the separate operations
            Add(new AppleInstallCommand());
            Add(new AppleUninstallCommand());
            Add(new AppleJustTestCommand());
            Add(new AppleJustRunCommand());

            // Commands for getting information
            Add(new AppleGetDeviceCommand());
            Add(new AppleGetStateCommand());

            // Commands for simulator management
            Add(new SimulatorsCommandSet());
        }
    }
}
