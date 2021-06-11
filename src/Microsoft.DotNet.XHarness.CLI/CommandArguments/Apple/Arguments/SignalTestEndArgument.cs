// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple
{
    /// <summary>
    /// Enables extra signaling between the TestRunner application and XHarness to work around problems in newer iOS.
    /// </summary>
    internal class SignalTestEndArgument : SwitchArgument
    {
        public SignalTestEndArgument() : base("signal-test-end", "Tells the TestRunner inside of the test application to signal back when tests have finished (iOS 14+ cannot detect this reliably otherwise)", false)
        {
        }
    }
}
