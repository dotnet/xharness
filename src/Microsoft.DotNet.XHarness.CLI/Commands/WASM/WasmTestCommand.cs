// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Wasm
{
    internal class WasmTestCommand : TestCommand
    {
        private const string CommandHelp = "Executes BCL xunit tests on WASM. It starts a JavaScript engine which calls a test runner inside of the WASM application.";
        private readonly WasmTestCommandArguments _arguments = new WasmTestCommandArguments();
        protected override TestCommandArguments TestArguments => _arguments;
        protected override string CommandUsage { get; } = "wasm test [OPTIONS] -- [ENGINE OPTIONS]";
        protected override string CommandDescription { get; } = CommandHelp;
        public IEnumerable<string> PassThroughArgs => PassThroughArguments;

        protected override async Task<ExitCode> InvokeInternal(ILogger logger)
        {
            var processManager = new MacOSProcessManager();

            // Expected syntax
            // xharness wasm test --engine=v8 --engine-arg=--expose_wasm --engine-arg=--some-other-thing --js-file=runtime.js -- --enable-gc --run WasmTestRunner.dll System.Buffers.Tests.dll
           
            var result = await processManager.ExecuteCommandAsync(
                _arguments.Engine.ToString(),
                _arguments.EngineArgs.Concat(new List<string>{ "--" }).Concat(PassThroughArgs).ToList(),
                new CallbackLog(m => logger.LogDebug(m)),
                _arguments.Timeout);

            return result.Succeeded ? ExitCode.SUCCESS : ExitCode.GENERAL_FAILURE;
        }

        public WasmTestCommand() : base(CommandHelp, true)
        {
        }
    }
}
