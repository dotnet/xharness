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
using System.IO;
using System.ComponentModel;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Wasm
{
    internal class WasmTestCommand : TestCommand
    {
        private const string CommandHelp = "Executes tests on WASM using a selected JavaScript engine";

        private readonly WasmTestCommandArguments _arguments = new WasmTestCommandArguments();

        private StreamWriter? _xmlResultsFileWriter = null;
        private bool _hasWasmStdoutPrefix = false;

        protected override TestCommandArguments TestArguments => _arguments;
        protected override string CommandUsage { get; } = "wasm test [OPTIONS] -- [ENGINE OPTIONS]";
        protected override string CommandDescription { get; } = CommandHelp;

        public WasmTestCommand() : base(CommandHelp, true)
        {
        }

        protected override async Task<ExitCode> InvokeInternal(ILogger logger)
        {
            var processManager = new MacOSProcessManager();

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

            try
            {
                var result = await processManager.ExecuteCommandAsync(
                    engineBinary,
                    engineArgs,
                    log: new CallbackLog(m => logger.LogInformation(m)),
                    stdout: new CallbackLog(m => WasmTestLogCallback(m, xmlResultsFilePath, logger)) { Timestamp = false /* we need the plain XML string so disable timestamp */ },
                    stderr: new CallbackLog(m => logger.LogError(m)),
                    _arguments.Timeout);

                return result.Succeeded ? ExitCode.SUCCESS : (result.TimedOut ? ExitCode.TIMED_OUT : ExitCode.GENERAL_FAILURE);
            }
            catch (Win32Exception e) when (e.Message == "No such file or directory")
            {
                logger.LogCritical($"The engine binary `{engineBinary}` was not found");
                return ExitCode.APP_LAUNCH_FAILURE;
            }
        }

        private void WasmTestLogCallback(string line, string xmlResultsFilePath, ILogger logger)
        {
            if (_xmlResultsFileWriter == null)
            {
                if (line.Contains("STARTRESULTXML"))
                {
                    _xmlResultsFileWriter = File.CreateText(xmlResultsFilePath);
                    _hasWasmStdoutPrefix = line.StartsWith("WASM: ");
                    return;
                }
                else if (line.Contains("Tests run:"))
                {
                    logger.LogInformation(line);
                }
                else
                {
                   logger.LogDebug(line);
                }
            }
            else
            {
                if (line.Contains("ENDRESULTXML"))
                {
                    _xmlResultsFileWriter.Flush();
                    _xmlResultsFileWriter.Dispose();
                    _xmlResultsFileWriter = null;
                    return;
                }
                _xmlResultsFileWriter.Write(_hasWasmStdoutPrefix ? line.Substring(6) : line);
            }
        }
    }
}
