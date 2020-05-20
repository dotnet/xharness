// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.WebAssembly;
using Microsoft.Extensions.Logging;
using Xunit.ConsoleClient;

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

            // FIXME This command line parsing should be moved to CLI command arguments
            //  Parameters: 
            // - directory name
            // - test assembly filename
            // - command line arguments

            var args = new List<string> { "test-assembly-filename" };
            var cmdline = new CmdLineParser(args.ToArray());

            var runner = new WasmRunner(cmdline.Project);
            var result = runner.Run();
            return Task.FromResult(result == 0 ? ExitCode.SUCCESS : ExitCode.GENERAL_FAILURE);
        }

        public WasmTestCommand() : base(CommandHelp)
        {
        }
    }
}
