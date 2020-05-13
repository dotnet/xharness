// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Android;
using Microsoft.DotNet.XHarness.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Wasm
{
    internal class WasmTestCommand : TestCommand
    {
        private readonly WasmTestCommandArguments _arguments = new WasmTestCommandArguments();

        protected override TestCommandArguments TestArguments => _arguments;

        protected override string CommandUsage { get; } = "wasm test [OPTIONS]";

        protected override string CommandDescription { get; } = "Executes BCL xunit tests on WASM";

        protected override Task<ExitCode> InvokeInternal(ILogger logger)
        {
            logger.LogDebug($"Wasm Test command called");
            return Task.FromResult(ExitCode.GENERAL_FAILURE);
        }
    }
}
