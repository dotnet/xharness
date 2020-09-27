// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

using SeleniumLogLevel = OpenQA.Selenium.LogLevel;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Wasm
{
    internal class WasmBrowserTestRunner
    {
        private readonly WasmTestBrowserCommandArguments _arguments;
        private readonly Action<string> _processLogMessage;
        private readonly ILogger _logger;
        private readonly IEnumerable<string> _passThroughArguments;

        // Messages from selenium prepend the url, and location where the message originated
        // Eg. `foo` becomes `http://localhost:8000/xyz.js 0:12 "foo"
        static readonly Regex s_consoleLogRegex = new Regex(@"^\s*[a-z]*://[^\s]+\s+\d+:\d+\s+""(.*)""\s*$", RegexOptions.Compiled);

        private TaskCompletionSource<bool> wasmExitReceivedTcs = new TaskCompletionSource<bool>();

        public WasmBrowserTestRunner(WasmTestBrowserCommandArguments arguments, IEnumerable<string> passThroughArguments,
                                            Action<string> processLogMessage, ILogger logger)
        {
            this._arguments = arguments;
            this._processLogMessage = processLogMessage;
            this._logger = logger;
            this._passThroughArguments = passThroughArguments;
        }

        public async Task<ExitCode> RunTestsWithWebDriver(DriverService driverService, IWebDriver driver)
        {
            var htmlFilePath = Path.Combine(_arguments.AppPackagePath, _arguments.HTMLFile);
            if (!File.Exists(htmlFilePath))
            {
                _logger.LogError($"Could not find html file {htmlFilePath}");
                return ExitCode.GENERAL_FAILURE;
            }

            var cts = new CancellationTokenSource();
            try
            {
                var consolePumpTcs = new TaskCompletionSource<bool>();
                string webServerAddr = await StartWebServer(
                    _arguments.AppPackagePath,
                    socket => RunConsoleMessagesPump(socket, consolePumpTcs, cts.Token),
                    cts.Token);

                string testUrl = BuildUrl(webServerAddr);

                var seleniumLogMessageTask = Task.Run(() => RunSeleniumLogMessagePump(driver, cts.Token), cts.Token);
                cts.CancelAfter(_arguments.Timeout);

                _logger.LogTrace($"Opening in browser: {testUrl}");
                driver.Navigate().GoToUrl(testUrl);

                var tasks = new Task[]
                {
                    wasmExitReceivedTcs.Task,
                    consolePumpTcs.Task,
                    seleniumLogMessageTask,
                    Task.Delay(_arguments.Timeout)
                };

                var task = await Task.WhenAny(tasks).ConfigureAwait(false);
                if (task == tasks[^1] || cts.IsCancellationRequested)
                {
                    if (driverService.IsRunning)
                    {
                        // Selenium isn't able to kill chrome in this case :/
                        int pid = driverService.ProcessId;
                        var p = Process.GetProcessById(pid);
                        if (p != null)
                        {
                            _logger.LogDebug($"Tests timed out. Killing chrome pid {pid}");
                            p.Kill(true);
                        }
                    }

                    // timed out
                    if (!cts.IsCancellationRequested)
                        cts.Cancel();
                    return ExitCode.TIMED_OUT;
                }

                if (task == wasmExitReceivedTcs.Task && wasmExitReceivedTcs.Task.IsCompletedSuccessfully)
                {
                    _logger.LogTrace($"Looking for `tests_done` element, to get the exit code");
                    var testsDoneElement = new WebDriverWait(driver, TimeSpan.FromSeconds(30))
                                                .Until (e => e.FindElement(By.Id("tests_done")));

                    if (int.TryParse(testsDoneElement.Text, out var code))
                    {
                        return (ExitCode) Enum.ToObject(typeof(ExitCode), code);
                    }

                    return ExitCode.RETURN_CODE_NOT_SET;
                }

                if (task.IsFaulted)
                {
                    _logger.LogDebug($"task faulted {task.Exception}");
                    throw task.Exception!;
                }

                return ExitCode.TIMED_OUT;
            }
            finally
            {
                if (!cts.IsCancellationRequested)
                {
                    cts.Cancel();
                }
            }
        }

        private async Task RunConsoleMessagesPump(WebSocket socket, TaskCompletionSource<bool> tcs, CancellationToken token)
        {
            byte[] buff = new byte[4000];
            var mem = new MemoryStream();
            try {
                while (!token.IsCancellationRequested)
                {
                    if (socket.State != WebSocketState.Open)
                    {
                        _logger.LogError($"DevToolsProxy: Socket is no longer open.");
                        tcs.SetResult(false);
                        return;
                    }

                    WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buff), token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        tcs.SetResult(false);
                        return;
                    }

                    mem.Write(buff, 0, result.Count);

                    if (result.EndOfMessage)
                    {
                        var line = Encoding.UTF8.GetString(mem.GetBuffer(), 0, (int)mem.Length);
                        _processLogMessage(line);

                        // the test runner writes this as the last line,
                        // after the tests have run, and the xml results file
                        // has been written to the console
                        if (line.StartsWith("WASM EXIT"))
                        {
                            wasmExitReceivedTcs.SetResult(true);
                        }
                    }

                    mem.SetLength(0);
                    mem.Seek(0, SeekOrigin.Begin);
                }

                // the result is not used
                tcs.SetResult(false);
            }
            catch (OperationCanceledException oce)
            {
                if (!token.IsCancellationRequested)
                    _logger.LogDebug($"RunConsoleMessagesPump cancelled: {oce}");
                tcs.SetResult(false);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
                throw;
            }
        }

        // This listens for any `console.log` messages.
        // Since we pipe messages from managed code, and console.* to the websocket,
        // this wouldn't normally get much. But listening on this to catch any messages
        // that we miss piping to the websocket.
        private void RunSeleniumLogMessagePump(IWebDriver driver, CancellationToken token)
        {
            try
            {
                ILogs logs = driver.Manage().Logs;
                while (!token.IsCancellationRequested)
                {
                    foreach (var logType in logs.AvailableLogTypes)
                    {
                        foreach (var logEntry in logs.GetLog(logType))
                        {
                            if (logEntry.Level == SeleniumLogLevel.Severe)
                            {
                                // These are errors from the browser, some of which might be
                                // thrown as part of tests. So, we can't differentiate when
                                // it is an error that we can ignore, vs one that should stop
                                // the execution completely.
                                //
                                // Note: these could be received out-of-order as compared to
                                // console messages via the websocket.
                                //
                                // (see commit message for more info)
                                _logger.LogError($"[out of order message from the {logType}]: {logEntry.Message}");
                                continue;
                            }

                            var match = s_consoleLogRegex.Match(Regex.Unescape(logEntry.Message));
                            string msg = match.Success ? match.Groups[1].Value : logEntry.Message;
                            _logger.LogDebug ($"{logType}: {msg}");
                        }
                    }
                }
            }
            catch (WebDriverException wde) when (wde.Message.Contains("timed out after"))
            { }
            catch (Exception ex)
            {
                _logger.LogDebug($"Failed trying to read log messages via selenium: {ex}");
                throw;
            }
        }

        private string BuildUrl(string webServerAddr)
        {
            var uriBuilder = new UriBuilder($"{webServerAddr}/{_arguments.HTMLFile}");
            var sb = new StringBuilder();
            foreach (var arg in _passThroughArguments)
            {
                if (sb.Length > 0)
                    sb.Append("&");

                sb.Append($"arg={HttpUtility.UrlEncode(arg)}");
            }

            uriBuilder.Query = sb.ToString();
            return uriBuilder.ToString();
        }

        private static async Task<string> StartWebServer(string contentRoot, Func<WebSocket, Task> onConsoleConnected, CancellationToken token)
        {
            var host = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(contentRoot)
                .UseStartup<WasmTestWebServerStartup>()
                .ConfigureLogging(logging =>
                {
                    logging.AddConsole().AddFilter(null, LogLevel.Warning);
                })
                .ConfigureServices((ctx, services) =>
                {
                    services.AddRouting();
                    services.Configure<WasmTestWebServerOptions>(ctx.Configuration);
                    services.Configure<WasmTestWebServerOptions>(options =>
                    {
                        options.OnConsoleConnected = onConsoleConnected;
                    });
                })
                .UseUrls("http://127.0.0.1:0")
                .Build();

            await host.StartAsync(token);
            return host.ServerFeatures
                    .Get<IServerAddressesFeature>()
                    .Addresses
                    .First();
        }
    }
}
