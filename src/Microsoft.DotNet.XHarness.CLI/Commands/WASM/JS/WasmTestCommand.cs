// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.Common.CLI.Commands;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Wasm
{
    internal class WasmTestCommand : XHarnessCommand
    {
        private const string CommandHelp = "Executes tests on WASM using a selected JavaScript engine";

        private readonly WasmTestCommandArguments _arguments = new WasmTestCommandArguments();

        protected override XHarnessCommandArguments Arguments => _arguments;
        protected override string CommandUsage { get; } = "wasm test [OPTIONS] -- [ENGINE OPTIONS]";
        protected override string CommandDescription { get; } = CommandHelp;

        public WasmTestCommand() : base("test", true, CommandHelp)
        {
        }

        protected override async Task<ExitCode> InvokeInternal(ILogger logger)
        {
            var processManager = ProcessManagerFactory.CreateProcessManager();

            var engineBinary = _arguments.Engine switch
            {
                JavaScriptEngine.V8 => "v8",
                JavaScriptEngine.JavaScriptCore => "jsc",
                JavaScriptEngine.SpiderMonkey => "sm",
                _ => throw new ArgumentException()
            };

            var engineArgs = new List<string>();

            if (_arguments.Engine == JavaScriptEngine.V8)
            {
                // v8 needs this flag to enable WASM support
                engineArgs.Add("--expose_wasm");
            }

            engineArgs.AddRange(_arguments.EngineArgs);
            engineArgs.Add(_arguments.JSFile);

            if (_arguments.Engine == JavaScriptEngine.V8 || _arguments.Engine == JavaScriptEngine.JavaScriptCore)
            {
                // v8/jsc want arguments to the script separated by "--", others don't
                engineArgs.Add("--");
            }

            engineArgs.AddRange(PassThroughArguments);

            var xmlResultsFilePath = Path.Combine(_arguments.OutputDirectory, "testResults.xml");
            File.Delete(xmlResultsFilePath);

            var stdoutFilePath = Path.Combine(_arguments.OutputDirectory, "wasm-console.log");
            File.Delete(stdoutFilePath);

            try
            {
                var logProcessor = new WasmTestMessagesProcessor(xmlResultsFilePath, stdoutFilePath, logger);
                var result = await processManager.ExecuteCommandAsync(
                    engineBinary,
                    engineArgs,
                    log: new CallbackLog(m => logger.LogInformation(m)),
                    stdoutLog: new CallbackLog(logProcessor.Invoke) { Timestamp = false /* we need the plain XML string so disable timestamp */ },
                    stderrLog: new CallbackLog(m => logger.LogError(m)),
                    _arguments.Timeout);
                if (result.ExitCode != _arguments.ExpectedExitCode)
                {
                    logger.LogError($"Application has finished with exit code {result.ExitCode} but {_arguments.ExpectedExitCode} was expected");
                        return ExitCode.GENERAL_FAILURE;
                    
                }
                else
                {
                    logger.LogInformation("Application has finished with exit code: " + result.ExitCode);
                    return ExitCode.SUCCESS;
                }
            }
            catch (Win32Exception e) when (e.NativeErrorCode == 2)
            {
                logger.LogCritical($"The engine binary `{engineBinary}` was not found");
                return ExitCode.APP_LAUNCH_FAILURE;
            }
        }
    }
}
