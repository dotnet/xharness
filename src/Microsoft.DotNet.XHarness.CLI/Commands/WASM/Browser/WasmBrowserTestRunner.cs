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
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Playwright;

using Microsoft.DotNet.XHarness.CLI.Commands;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.Logging;

using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Wasm;

internal class WasmBrowserTestRunner
{
    private readonly WasmTestBrowserCommandArguments _arguments;
    private readonly ILogger _logger;
    private readonly IEnumerable<string> _passThroughArguments;
    private readonly WasmTestMessagesProcessor _messagesProcessor;

    // Messages from selenium prepend the url, and location where the message originated
    // Eg. `foo` becomes `http://localhost:8000/xyz.js 0:12 "foo"
    static readonly Regex s_consoleLogRegex = new(@"^\s*[a-z]*://[^\s]+\s+\d+:\d+\s+""(.*)""\s*$", RegexOptions.Compiled);
    private static readonly Regex s_payloadRegex = new Regex("\"payload\":\"(?<payload>[^\"]*)\"", RegexOptions.Compiled);
    private static readonly Regex s_exitRegex = new Regex("WASM EXIT (?<exitCode>-?[0-9]+)$");
    

    public WasmBrowserTestRunner(WasmTestBrowserCommandArguments arguments, IEnumerable<string> passThroughArguments,
                                        WasmTestMessagesProcessor messagesProcessor, ILogger logger)
    {
        _arguments = arguments;
        _logger = logger;
        _passThroughArguments = passThroughArguments;
        _messagesProcessor = messagesProcessor;
    }
    public async Task<ExitCode> RunTestsWithPlaywright(IBrowser browser, IBrowserContext? context)
    {
        var htmlFilePath = Path.Combine(_arguments.AppPackagePath, _arguments.HTMLFile.Value);
        if (!File.Exists(htmlFilePath))
        {
            _logger.LogError($"Could not find html file {htmlFilePath}");
            return ExitCode.GENERAL_FAILURE;
        }

        var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        try
        {
            var logProcessorTask = Task.Run(() => _messagesProcessor.RunAsync(cts.Token));
            var webServerOptions = WebServer.TestWebServerOptions.FromArguments(_arguments);
            webServerOptions.ContentRoot = _arguments.AppPackagePath;
            ServerURLs serverURLs = await WebServer.Start(
                webServerOptions,
                _logger,
                cts.Token);

            string testUrl = BuildUrl(serverURLs);
            
            var page = await browser.NewPageAsync();
            var exitCodeTcs = new TaskCompletionSource<int>();
            SetupPageLogs(page, exitCodeTcs, cancellationToken);
            cts.CancelAfter(_arguments.Timeout);

            _logger.LogDebug($"Opening in browser: {testUrl}");
        
            await page.GotoAsync(testUrl);

            var tasks = new Task[]
            {
                exitCodeTcs.Task,
                Task.Delay(_arguments.Timeout),
            };

            var task = await Task.WhenAny(tasks).ConfigureAwait(false);

            if (task == tasks[^1] || cts.IsCancellationRequested)
            {
                try
                {
                    if (context is not null)
                    {
                        var ctsCloseTabs = new CancellationTokenSource();
                        ctsCloseTabs.CancelAfter(10000);
                        await CloseAllTabs(context, ctsCloseTabs);
                    }
                    await browser.CloseAsync();
                    await browser.DisposeAsync();
                }
                catch (Exception e)
                {
                    _logger.LogError($"Tests timed out. Error while closing browser: {e}");
                }

                // timed out
                if (!cts.IsCancellationRequested)
                    cts.Cancel();
                return ExitCode.TIMED_OUT;
            }

            if (task.IsFaulted)
            {
                _logger.LogDebug($"task faulted {task.Exception}");
                throw task.Exception!;
            }

            try
            {
                var exitCode = await exitCodeTcs.Task;
                return (ExitCode)exitCode;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error while processing exit code: {e}");
                return ExitCode.GENERAL_FAILURE;
            }
        }
        finally
        {
            if (!cts.IsCancellationRequested)
            {
                cts.Cancel();
            }
        }
    }
    
    private async Task CloseAllTabs(IBrowserContext context, CancellationTokenSource cts)
    {
        var pages = context.Pages;
        _logger.LogInformation($"Closing {pages.Count} browser tabs before quitting.");
        foreach (var page in pages)
        {
            if (cts.IsCancellationRequested)
            {
                _logger.LogInformation($"Timeout while trying to close tabs, {context.Pages.Count} is left open before quitting.");
                break;
            }
            await page.CloseAsync();
        }
    }

    private void SetupPageLogs(IPage page, TaskCompletionSource<int> exitCodeTcs, CancellationToken token)
    {
        page.Console += (_, msg) =>
        {
            try
            {
                if (token.IsCancellationRequested || string.IsNullOrEmpty(msg.Text))
                {
                    return;
                }

                string message = msg.Text;
                if (msg.Type == "error")
                {
                    _logger.LogError($"[out of order message from the console]: {message}");
                    return;
                }

                // console.logs and console.infos are in json format
                string payload = TryGetPayloadFromJson(message);
                if (msg.Type == "log")
                {
                    int? exitCode = GetExitCode(payload);
                    if (exitCode is not null)
                    {
                        exitCodeTcs.TrySetResult(exitCode.Value);
                        return;
                    }
                }
                else if (msg.Type == "debug")
                {
                    _logger.LogDebug($"Debug: {payload}");
                    return;
                }

                _messagesProcessor.Invoke(payload);
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Failed trying to read log messages via Playwright: {ex}");
                throw;
            }
        };
        page.PageError += (_, msg) => {
            _logger.LogError($"Page error: {msg}");
        };
        page.FrameDetached += (_, msg) => {
            _logger.LogError($"Frame detached: {msg.Name}");
        };
    }

    private string TryGetPayloadFromJson(string message)
    {
        Match match = s_payloadRegex.Match(message);
        return match.Success ? match.Groups["payload"].Value : message;
    }

    private int? GetExitCode(string message)
    {
        Match m = s_exitRegex.Match(message);
        return m.Success ? int.Parse(m.Groups["exitCode"].Value) : null;
    }

    private string BuildUrl(ServerURLs serverURLs)
    {
        var uriBuilder = new UriBuilder($"{serverURLs.Http}/{_arguments.HTMLFile}");
        var sb = new StringBuilder();

        if (_arguments.DebuggerPort.Value != null)
            sb.Append($"arg=--debug");

        foreach (var envVariable in _arguments.WebServerHttpEnvironmentVariables.Value)
        {
            if (sb.Length > 0)
                sb.Append('&');
            sb.Append($"arg={HttpUtility.UrlEncode($"--setenv={envVariable}={serverURLs!.Http}")}");
        }

        if (_arguments.WebServerUseHttps)
        {
            foreach (var envVariable in _arguments.WebServerHttpsEnvironmentVariables.Value)
            {
                if (sb.Length > 0)
                    sb.Append('&');
                sb.Append($"arg={HttpUtility.UrlEncode($"--setenv={envVariable}={serverURLs!.Https}")}");
            }
        }

        foreach (var arg in _passThroughArguments)
        {
            if (sb.Length > 0)
                sb.Append('&');

            sb.Append($"arg={HttpUtility.UrlEncode(arg)}");
        }

        if (sb.Length > 0)
            sb.Append('&');

        sb.Append($"arg=-verbosity&arg={VerbosityToString()}");

        uriBuilder.Query = sb.ToString();
        return uriBuilder.ToString();
    }

    // MinimumLogLevel.Critical,
    // MinimumLogLevel.Error,
    // MinimumLogLevel.Warning,
    // MinimumLogLevel.Info,
    // MinimumLogLevel.Debug,
    // MinimumLogLevel.Verbose
    private string VerbosityToString() => _arguments.Verbosity.Value switch
    {
        LogLevel.Trace => "Verbose",
        LogLevel.Debug => "Debug",
        LogLevel.Information => "Info",
        LogLevel.Warning => "Warning",
        LogLevel.Error => "Error",
        LogLevel.Critical => "Critical",
        LogLevel.None => "Critical",
        _ => throw new NotSupportedException($"The value '{_arguments.Verbosity.Value}' is not supported in conversion to MinimumLogLevel")
    };
}
