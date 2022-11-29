// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasi;

internal class WasmFileArgument : RequiredStringArgument
{
    public WasmFileArgument(string defaultValue)
        : base("wasm-file=", "Main wasm file to be run on the wasmtime engine. Default is " + defaultValue, defaultValue)
    {
    }
}
