// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm
{
    internal class DebuggerPortArgument : IntArgument
    {
        public DebuggerPortArgument()
            : base("debugger=|d=", "Run browser in debug mode, with a port to listen on. Default port number is 9222", 9222)
        {
        }
    }
}
