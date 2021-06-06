// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple
{
    /// <summary>
    /// Path where the outputs of execution will be stored
    /// </summary>
    internal class OutputDirectoryArgument : PathArgument
    {
        public OutputDirectoryArgument() : base("output-directory=|o=", "Directory where logs and results will be saved")
        {
        }
    }
}
