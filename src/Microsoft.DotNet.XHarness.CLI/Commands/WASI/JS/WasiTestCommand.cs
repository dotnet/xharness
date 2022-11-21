namespace Microsoft.DotNet.XHarness.CLI.Commands.Wasi;
internal class WasiTestCommand : XHarnessCommand<WasmTestCommandArguments>
{
    private const string CommandHelp = "Executes tests on WASI using a selected JavaScript engine";

    protected override WasiTestCommandArguments Arguments { get; } = new();
    protected override string CommandUsage { get; } = "wasi test [OPTIONS] -- [ENGINE OPTIONS]";
    protected override string CommandDescription { get; } = CommandHelp;

    public WasiTestCommand() : base(TargetPlatform.WASI, "test", true, new ServiceCollection(), CommandHelp)
    {
    }
//didn't check yet
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

        string engineBinary = Arguments.Engine.Value switch
        {
            JavaScriptEngine.V8 => "v8",
            _ => throw new ArgumentException("Engine not set")
        };

        if (!string.IsNullOrEmpty(Arguments.EnginePath.Value))
        {
            engineBinary = Arguments.EnginePath.Value;
            if (Path.IsPathRooted(engineBinary) && !File.Exists(engineBinary))
                throw new ArgumentException($"Could not find js engine at the specified path - {engineBinary}");
        }
        /*else
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (engineBinary.Equals("node"))
                    engineBinary = FindEngineInPath(engineBinary + ".exe"); // NodeJS ships as .exe rather than .cmd
                else
                    engineBinary = FindEngineInPath(engineBinary + ".cmd");
            }
        }*/

        logger.LogInformation($"Using js engine {Arguments.Engine.Value} from path {engineBinary}");
        await PrintVersionAsync(Arguments.Engine.Value.Value, engineBinary);

        var cts = new CancellationTokenSource();
        try
        {
            ServerURLs? serverURLs = null;
            if (Arguments.WebServerMiddlewarePathsAndTypes.Value.Count > 0)
            {
                serverURLs = await WebServer.Start(
                    Arguments,
                    null,
                    logger,
                    null,
                    cts.Token);
                cts.CancelAfter(Arguments.Timeout);
            }

            var engineArgs = new List<string>();
            
            // v8 needs this flag to enable WASI support
            engineArgs.Add("--expose_wasi");

            engineArgs.AddRange(Arguments.EngineArgs.Value);
            engineArgs.Add(Arguments.JSFile);

            // v8 want arguments to the script separated by "--", others don't
            engineArgs.Add("--");

            if (Arguments.WebServerMiddlewarePathsAndTypes.Value.Count > 0)
            {
                foreach (var envVariable in Arguments.WebServerHttpEnvironmentVariables.Value)
                {
                    engineArgs.Add($"--setenv={envVariable}={serverURLs!.Http}");
                }
                if (Arguments.WebServerUseHttps)
                {
                    foreach (var envVariable in Arguments.WebServerHttpsEnvironmentVariables.Value)
                    {
                        engineArgs.Add($"--setenv={envVariable}={serverURLs!.Https}");
                    }
                }
            }

            engineArgs.AddRange(PassThroughArguments);

            var xmlResultsFilePath = Path.Combine(Arguments.OutputDirectory, "testResults.xml");
            File.Delete(xmlResultsFilePath);

            var stdoutFilePath = Path.Combine(Arguments.OutputDirectory, "wasi-console.log");
            File.Delete(stdoutFilePath);

            var symbolicator = WasiSymbolicatorBase.Create(Arguments.SymbolicatorArgument.GetLoadedTypes().FirstOrDefault(),
                                                           Arguments.SymbolMapFileArgument,
                                                           Arguments.SymbolicatePatternsFileArgument,
                                                           logger);

            var logProcessor = new WasmTestMessagesProcessor(xmlResultsFilePath,
                                                             stdoutFilePath,
                                                             logger,
                                                             Arguments.ErrorPatternsFile,
                                                             symbolicator);
            var logProcessorTask = Task.Run(() => logProcessor.RunAsync(cts.Token));

            var processTask = processManager.ExecuteCommandAsync(
                engineBinary,
                engineArgs,
                log: new CallbackLog(m => logger.LogInformation(m)),
                stdoutLog: new CallbackLog(msg => logProcessor.Invoke(msg)),
                stderrLog: new CallbackLog(logProcessor.ProcessErrorMessage),
                Arguments.Timeout);

            var tasks = new Task[]
            {
                logProcessorTask,
                processTask,
                Task.Delay(Arguments.Timeout)
            };

            var task = await Task.WhenAny(tasks).ConfigureAwait(false);
            if (task == tasks[^1] || cts.IsCancellationRequested || task.IsCanceled)
            {
                logger.LogError($"Tests timed out after {((TimeSpan)Arguments.Timeout).TotalSeconds}secs");
                if (!cts.IsCancellationRequested)
                    cts.Cancel();

                return ExitCode.TIMED_OUT;
            }

            if (task.IsFaulted)
            {
                logger.LogError($"task faulted {task.Exception}");
                throw task.Exception!;
            }

            // if the log processor completed without errors, then the
            // process should be done too, or about to be done!
            var result = await processTask;
            ExitCode logProcessorExitCode = await logProcessor.CompleteAndFlushAsync();

            if (result.ExitCode != Arguments.ExpectedExitCode)
            {
                logger.LogError($"Application has finished with exit code {result.ExitCode} but {Arguments.ExpectedExitCode} was expected");
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
                // return SUCCESS if logProcess also returned SUCCESS
                return logProcessorExitCode;
            }
        }
        catch (Win32Exception e) when (e.NativeErrorCode == 2)
        {
            logger.LogCritical($"The engine binary `{engineBinary}` was not found");
            return ExitCode.APP_LAUNCH_FAILURE;
        }
        finally
        {
            if (!cts.IsCancellationRequested)
            {
                cts.Cancel();
            }
        }

        Task PrintVersionAsync(JavaScriptEngine engine, string engineBinary)
        {
            return processManager.ExecuteCommandAsync(
                            engineBinary,
                            new[] { "-e", "console.log(`V8 version: ${this.version()}`)" },
                            log: new CallbackLog(m => logger.LogDebug(m.Trim())),
                            stdoutLog: new CallbackLog(msg => logger.LogInformation(msg.Trim())),
                            stderrLog: new CallbackLog(msg => logger.LogError(msg.Trim())),
                            TimeSpan.FromSeconds(10));

            return Task.CompletedTask;
        }
    }

}