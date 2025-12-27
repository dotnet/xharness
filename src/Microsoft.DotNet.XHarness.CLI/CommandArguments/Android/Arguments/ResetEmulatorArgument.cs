// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;

/// <summary>
/// Stops all running emulators and wipes data before starting a new emulator.
/// Matches the iOS --reset-simulator behavior for consistency.
/// </summary>
internal class ResetEmulatorArgument : SwitchArgument
{
    public ResetEmulatorArgument()
        : base("reset-emulator", "Stops all emulators and wipes data before starting. Stops it after completion too", false)
    {
    }
}
