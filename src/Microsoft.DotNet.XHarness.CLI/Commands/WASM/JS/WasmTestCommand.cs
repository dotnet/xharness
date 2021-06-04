// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.Common.CLI.Commands;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Wasm
{
    internal class WasmTestCommand : XHarnessCommand
    {
        private const string CommandHelp = "Executes tests on WASM using a selected JavaScript engine";

        private readonly WasmTestCommandArguments _arguments = new();

        protected override XHarnessCommandArguments Arguments => _arguments;
        protected override string CommandUsage { get; } = "wasm test [OPTIONS] -- [ENGINE OPTIONS]";
        protected override string CommandDescription { get; } = CommandHelp;

        public WasmTestCommand() : base("test", true, CommandHelp)
        {
        }

        private static string FindEngineInPath(string engineBinary)
        {
            if (File.Exists(engineBinary) || Path.IsPathRooted(engineBinary))
                return engineBinary;

            var path = Environment.GetEnvironmentVariable("PATH");

            if (path == null)
                return engineBinary;

            foreach (var folder in path.Split(Path.PathSeparator))
            {
                var fullPath = Path.Combine(folder, engineBinary);
                if (File.Exists(fullPath))
                    return fullPath;
            }

            return engineBinary;
        }

        protected override async Task<ExitCode> InvokeInternal(ILogger logger)
        {
            var processManager = ProcessManagerFactory.CreateProcessManager();

            var engineBinary = _arguments.Engine switch
            {
                JavaScriptEngine.V8 => "v8",
                JavaScriptEngine.JavaScriptCore => "jsc",
                JavaScriptEngine.SpiderMonkey => "sm",
                _ => throw new ArgumentException("Engine not set")
            };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                engineBinary = FindEngineInPath(engineBinary + ".cmd");

            var webServerCts = new CancellationTokenSource();
            try
            {
                ServerURLs? serverURLs = null;
                if (_arguments.WebServerMiddlewarePathsAndTypes.Count > 0)
                {
                    serverURLs = await WebServer.Start(
                        _arguments, logger,
                        null,
                        webServerCts.Token);
                    webServerCts.CancelAfter(_arguments.Timeout);
                }

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

                if (_arguments.WebServerMiddlewarePathsAndTypes.Count > 0)
                {
                    foreach (var envVariable in _arguments.SetWebServerEnvironmentVariablesHttp)
                    {
                        engineArgs.Add($"--setenv={envVariable}={serverURLs!.Http}");
                    }

                    foreach (var envVariable in _arguments.SetWebServerEnvironmentVariablesHttps)
                    {
                        engineArgs.Add($"--setenv={envVariable}={serverURLs!.Https}");
                    }
                }

                engineArgs.AddRange(PassThroughArguments);

                var xmlResultsFilePath = Path.Combine(_arguments.OutputDirectory, "testResults.xml");
                File.Delete(xmlResultsFilePath);

                var stdoutFilePath = Path.Combine(_arguments.OutputDirectory, "wasm-console.log");
                File.Delete(stdoutFilePath);

                var logProcessor = new WasmTestMessagesProcessor(xmlResultsFilePath, stdoutFilePath, logger, _arguments.ErrorPatternsFile);
                var result = await processManager.ExecuteCommandAsync(
                    engineBinary,
                    engineArgs,
                    log: new CallbackLog(m => logger.LogInformation(m)),
                    stdoutLog: new CallbackLog(logProcessor.Invoke),
                    stderrLog: new CallbackLog(logProcessor.ProcessErrorMessage),
                    _arguments.Timeout);
                
                if (result.ExitCode != _arguments.ExpectedExitCode)
                {
                    logger.LogError($"Application has finished with exit code {result.ExitCode} but {_arguments.ExpectedExitCode} was expected");
                    return ExitCode.GENERAL_FAILURE;
                }
                else
                {
                    if (logProcessor.LineThatMatchedErrorPattern != null)
                    {
                        logger.LogError("Application exited with the expected exit code: {result.ExitCode}."
                                        + $" But found a line matching an error pattern: {logProcessor.LineThatMatchedErrorPattern}");
                        return ExitCode.APP_CRASH;
                    }

                    logger.LogInformation("Application has finished with exit code: " + result.ExitCode);
                    return ExitCode.SUCCESS;
                }
            }
            catch (Win32Exception e) when (e.NativeErrorCode == 2)
            {
                logger.LogCritical($"The engine binary `{engineBinary}` was not found");
                return ExitCode.APP_LAUNCH_FAILURE;
            }
            finally
            {
                if (!webServerCts.IsCancellationRequested)
                {
                    webServerCts.Cancel();
                }
            }
        }
    }
}
