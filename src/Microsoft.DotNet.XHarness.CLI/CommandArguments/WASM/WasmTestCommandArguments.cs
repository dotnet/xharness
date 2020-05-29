// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm
{
    internal class WasmTestCommandArguments : TestCommandArguments
    {
        protected override OptionSet GetTestCommandOptions() => new OptionSet();
    }
}
