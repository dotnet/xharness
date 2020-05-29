// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Wasm
{
    internal class WasmTestCommand : TestCommand
    {
        private const string CommandHelp = "Executes BCL xunit tests on WASM";
        private readonly WasmTestCommandArguments _arguments = new WasmTestCommandArguments();
        protected override TestCommandArguments TestArguments => _arguments;
        protected override string CommandUsage { get; } = "wasm test [OPTIONS]";
        protected override string CommandDescription { get; } = CommandHelp;

        protected override Task<ExitCode> InvokeInternal(ILogger logger)
        {
            logger.LogDebug($"Wasm Test command called");

            var result = 0;
            return Task.FromResult(result == 0 ? ExitCode.SUCCESS : ExitCode.GENERAL_FAILURE);
        }

        public WasmTestCommand() : base(CommandHelp)
        {
        }
    }
}
