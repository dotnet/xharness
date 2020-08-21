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

        private bool _endResultXmlStringSeen = false;

        public WasmBrowserTestRunner(WasmTestBrowserCommandArguments arguments, IEnumerable<string> passThroughArguments,
                                            Action<string> processLogMessage, ILogger logger)
        {
            this._arguments = arguments;
            this._processLogMessage = processLogMessage;
            this._logger = logger;
            this._passThroughArguments = passThroughArguments;
        }

        public async Task<ExitCode> RunTestsWithWebDriver(IWebDriver driver)
        {
            var htmlFilePath = Path.Combine(_arguments.AppPackagePath, _arguments.HTMLFile);
            if (!File.Exists(htmlFilePath))
            {
                _logger.LogError($"Could not find html file {htmlFilePath}");
                return ExitCode.GENERAL_FAILURE;
            }

            var webServerCts = new CancellationTokenSource();
            var pumpLogMessageCts = new CancellationTokenSource();
            try
            {
                string webServerAddr = await StartWebServer(_arguments.AppPackagePath, webServerCts.Token);
                var testUrl = BuildUrl(webServerAddr);

                pumpLogMessageCts.CancelAfter(_arguments.Timeout);

                _logger.LogTrace($"Opening in browser: {testUrl}");
                driver.Navigate().GoToUrl(testUrl);

                var testsDoneTask = Task.Run (() =>
                    new WebDriverWait (driver, _arguments.Timeout)
                        .Until (e => e.FindElement(By.Id("tests_done")))
                );

                var logPumpingTask = RunLogMessagesPump(driver, pumpLogMessageCts.Token);
                var task = await Task.WhenAny(logPumpingTask, testsDoneTask)
                                .ConfigureAwait(false);

                if (task == logPumpingTask && logPumpingTask.IsFaulted)
                {
                    throw logPumpingTask.Exception!;
                }

                if (task == testsDoneTask && testsDoneTask.IsCompletedSuccessfully)
                {
                    // WebDriverWait could return before we could get all
                    // the messages, so pump any remaining ones.
                    // This can also be a bit delayed, if the console is very
                    // frequently being written to, in the browser
                    int count = 0;
                    do
                    {
                        await Task.Delay(500);
                        PumpLogMessages(driver);
                    } while (count < 5 && !_endResultXmlStringSeen);

                    var elem = testsDoneTask.Result;
                    if (int.TryParse(elem.Text, out var code))
                    {
                        return (ExitCode) Enum.ToObject(typeof(ExitCode), code);
                    }

                    return ExitCode.RETURN_CODE_NOT_SET;
                }

                if (task.IsFaulted)
                {
                    _logger.LogDebug(task.Exception!, "Waiting for tests failed");
                    throw task.Exception!;
                }

                // WebDriverWait could return before we could get all
                // the messages, so pump any remaining ones.
                await Task.Delay(1000);
                PumpLogMessages(driver);

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
            }
        }

        private async Task RunLogMessagesPump(IWebDriver driver, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    PumpLogMessages(driver);
                    if (driver.CurrentWindowHandle == null) {
                        // Accessing this property will throw, if the browser has crashed
                    }

                    await Task.Delay(100, token).ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException)
            {
                // Ignore
            }
        }

        private void PumpLogMessages(IWebDriver driver)
        {
            foreach (var logEntry in driver.Manage().Logs.GetLog(LogType.Browser))
            {
                if (logEntry.Level == SeleniumLogLevel.Severe)
                {
                    // throw on driver errors, or other errors that show up
                    // in the console log
                    throw new ArgumentException(logEntry.Message);
                }

                var match = s_consoleLogRegex.Match(Regex.Unescape(logEntry.Message));
                string msg = match.Success ? match.Groups[1].Value : logEntry.Message;
                msg += Environment.NewLine;

                _processLogMessage(msg);

                // the test runner writes this as the last line,
                // after the tests have run, and the xml results file
                // has been written to the console
                if (msg.Contains("ENDRESULTXML"))
                {
                    _endResultXmlStringSeen = true;
                }
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

        private static async Task<string> StartWebServer(string contentRoot, CancellationToken token)
        {
            var host = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(contentRoot)
                .UseStartup<WasmTestWebServerStartup>()
                .ConfigureLogging(logging => {
                    logging.AddConsole().AddFilter(null, LogLevel.Warning);
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
