// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.Common.Execution;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple
{
    /// <summary>
    /// Path to the mlaunch binary.
    /// Default comes from the NuGet.
    /// </summary>
    internal class MlaunchArgument : PathArgument
    {
        public MlaunchArgument() : base("mlaunch=", "Path to the mlaunch binary")
        {
            Path = MacOSProcessManager.DetectMlaunchPath();
        }
    }
}
