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
            Add(new AppleTestCommand());
            Add(new AppleRunCommand());
            Add(new AppleGetDeviceCommand());
            Add(new AppleInstallCommand());
            Add(new AppleUninstallCommand());
            Add(new AppleGetStateCommand());
            Add(new SimulatorsCommandSet());
        }
    }
}
