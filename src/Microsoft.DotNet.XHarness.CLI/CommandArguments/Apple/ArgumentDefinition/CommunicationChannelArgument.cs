﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.Apple;
using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple
{
    /// <summary>
    /// The way the simulator/device talks back to XHarness.
    /// </summary>
    internal class CommunicationChannelArgument : ArgumentDefinition<CommunicationChannel>
    {
        public CommunicationChannelArgument()
            : base("communication-channel=", "The communication channel to use to communicate with the default", CommunicationChannel.UsbTunnel)
        {
        }

        public override void Action(string argumentValue)
        {
            Value = ParseArgument<CommunicationChannel>("communication-channel", argumentValue);
        }
    }
}
