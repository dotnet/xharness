// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
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

using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

using SLogLevel = OpenQA.Selenium.LogLevel;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Wasm
{
    internal class WasmBrowserTestRunner
    {
        private readonly WasmTestCommandArguments _arguments;
        private readonly Action<string> _processLogMessage;
        private readonly ILogger _logger;
        private readonly IEnumerable<string> _passThroughArguments;

        // Messages from selenium prepend the url, and location where the message originated
        // Eg. `foo` becomes `http://localhost:8000/xyz.js 0:12 "foo"
        static readonly Lazy<Regex> s_consoleLogRegex = new Lazy<Regex>(() => new Regex(@"^\s*[a-z]*://[^\s]+\s+\d+:\d+\s+""(.*)""\s*$"));

        public WasmBrowserTestRunner(WasmTestCommandArguments arguments, IEnumerable<string> passThroughArguments,
                                            Action<string> processLogMessage, ILogger logger)
        {
            this._arguments = arguments;
            this._processLogMessage = processLogMessage;
            this._logger = logger;
            this._passThroughArguments = passThroughArguments;
        }

        public async Task<ExitCode> RunWithChrome()
        {
            var options = new ChromeOptions();
            options.SetLoggingPreference(LogType.Browser, SLogLevel.All);
            options.AddArguments(new List<string>(_arguments.EngineArgs)
            {
                "--incognito",
                "--headless"
            });

            return await RunTestsWithWebDriver(new ChromeDriver(options));
        }

        private async Task<ExitCode> RunTestsWithWebDriver(IWebDriver driver)
        {
            CancellationTokenSource webServerCts = new CancellationTokenSource();
            string webServerAddr = await StartWebServer(webServerCts.Token);

            var testUrl = BuildUrl(webServerAddr);
            _logger.LogTrace($"Opening in browser: {testUrl}");
            driver.Navigate().GoToUrl(testUrl);

            var pumpLogMessageCts = new CancellationTokenSource();
            var testsTcs = new TaskCompletionSource<ExitCode>();
            try {
                var logPumpingTask = RunLogMessagesPump(driver, testsTcs, pumpLogMessageCts.Token);
                var task = await Task.WhenAny(
                                    logPumpingTask,
                                    testsTcs.Task,
                                    Task.Delay(_arguments.Timeout, pumpLogMessageCts.Token))
                                .ConfigureAwait(false);

                if (task == logPumpingTask && logPumpingTask.IsFaulted)
                {
                    throw logPumpingTask.Exception!;
                }

                if (task == testsTcs.Task && testsTcs.Task.IsCompleted)
                {
                    return testsTcs.Task.Result;
                }

                return ExitCode.TIMED_OUT;
            }
            finally
            {
                if (!pumpLogMessageCts.IsCancellationRequested)
                {
                    pumpLogMessageCts.Cancel();
                }

                if (!webServerCts.IsCancellationRequested)
                {
                    webServerCts.Cancel();
                }
                driver.Quit();
            }
        }

        private async Task RunLogMessagesPump(IWebDriver driver, TaskCompletionSource<ExitCode> taskCompletion, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && !taskCompletion.Task.IsCompleted)
                {
                    foreach (var logEntry in driver.Manage().Logs.GetLog(LogType.Browser))
                    {
                        if (logEntry.Level == SLogLevel.Severe)
                        {
                            // throw on driver errors, or other errors that show up
                            // in the console log
                            throw new ArgumentException(logEntry.Message);
                        }

                        var match = s_consoleLogRegex.Value.Match(Regex.Unescape(logEntry.Message));
                        string msg = match.Success ? match.Groups[1].Value : logEntry.Message;
                        msg += Environment.NewLine;

                        _processLogMessage(msg);

                        if (!msg.StartsWith("WASM EXIT "))
                        {
                            continue;
                        }

                        if (int.TryParse(msg.Substring("WASM EXIT ".Length).Trim(), out var code))
                        {
                            taskCompletion.SetResult((ExitCode) Enum.ToObject(typeof(ExitCode), code));
                        }
                        else
                        {
                            _logger.LogDebug($"Got an unknown exit code in msg: '{msg}'");
                            taskCompletion.SetResult(ExitCode.TESTS_FAILED);
                        }
                    }

                    if (driver.CurrentWindowHandle == null) {
                        // Accessing this property will throw, if chrome has crashed
                    }

                    await Task.Delay(100, token).ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException)
            {
                // Ignore
            }
        }

        private string BuildUrl(string webServerAddr)
        {
            var uriBuilder = new UriBuilder($"{webServerAddr}/index.html");
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

        private static async Task<string> StartWebServer(CancellationToken token)
        {
            var host = new WebHostBuilder()
                .UseSetting("UseIISIntegration", false.ToString())
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<WasmTestWebServerStartup>()
                .ConfigureLogging(logging => {
                    logging.AddConsole().AddFilter(null, LogLevel.Debug);
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
