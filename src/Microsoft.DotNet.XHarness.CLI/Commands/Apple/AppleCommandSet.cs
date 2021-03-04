// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Apple
{
    // Main iOS command set that contains the plaform specific commands. This allows the command line to
    // support different options in different platforms.
    // Whenever the behavior does match, the goal is to have the same arguments for both platforms
    public class AppleCommandSet : CommandSet
    {
        public AppleCommandSet() : base("apple")
        {
            // commond verbs shared with android. We should think a smart way to do this
            Add(new AppleTestCommand());
            Add(new AppleRunCommand());
            Add(new AppleGetStateCommand());
        }
    }
}
