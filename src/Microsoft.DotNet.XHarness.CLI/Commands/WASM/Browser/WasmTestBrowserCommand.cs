// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Microsoft.DotNet.XHarness.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.CLI.Commands;
using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;
using Microsoft.Extensions.Logging;

using OpenQA.Selenium.Chrome;
using SeleniumLogLevel = OpenQA.Selenium.LogLevel;
using OpenQA.Selenium;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Wasm
{
    internal class WasmTestBrowserCommand : XHarnessCommand
    {
        private const string CommandHelp = "Executes tests on WASM using a browser";

        private readonly WasmTestBrowserCommandArguments _arguments = new WasmTestBrowserCommandArguments();

        protected TestCommandArguments TestArguments => _arguments;
        protected override string CommandUsage { get; } = "wasm test-browser [OPTIONS] -- [BROWSER OPTIONS]";
        protected override string CommandDescription { get; } = CommandHelp;

        protected override XHarnessCommandArguments Arguments => TestArguments;

        public WasmTestBrowserCommand() : base("test-browser", allowsExtraArgs: true, CommandHelp)
        {
        }

        protected override async Task<ExitCode> InvokeInternal(ILogger logger)
        {
            var xmlResultsFilePath = Path.Combine(_arguments.OutputDirectory, "testResults.xml");
            File.Delete(xmlResultsFilePath);

            var stdoutFilePath = Path.Combine(_arguments.OutputDirectory, "wasm-console.log");
            File.Delete(stdoutFilePath);

            var logProcessor = new WasmTestMessagesProcessor(xmlResultsFilePath, stdoutFilePath, logger);
            var runner = new WasmBrowserTestRunner(
                                _arguments,
                                PassThroughArguments,
                                logProcessor,
                                logger);

            var (driverService, driver) = GetChromeDriver(logger);
            try
            {
                return await runner.RunTestsWithWebDriver(driverService, driver);
            }
            finally
            {
                driverService.Dispose();
                driver.Dispose();
            }
        }

        private (DriverService, IWebDriver) GetChromeDriver(ILogger logger)
        {
            var options = new ChromeOptions();
            options.SetLoggingPreference(LogType.Browser, SeleniumLogLevel.All);

            options.AddArguments(new List<string>(_arguments.BrowserArgs)
            {
                "--incognito",
                "--headless",

                // added based on https://github.com/puppeteer/puppeteer/blob/main/src/node/Launcher.ts#L159-L181
                "--enable-features=NetworkService,NetworkServiceInProcess",
                "--disable-background-timer-throttling",
                "--disable-backgrounding-occluded-windows",
                "--disable-breakpad",
                "--disable-component-extensions-with-background-pages",
                "--disable-dev-shm-usage",
                "--disable-extensions",
                "--disable-features=TranslateUI",
                "--disable-ipc-flooding-protection",
                "--disable-renderer-backgrounding",
                "--force-color-profile=srgb",
                "--metrics-recording-only"
            });

            var driverService = ChromeDriverService.CreateDefaultService();
            driverService.EnableVerboseLogging = true;
            driverService.LogPath = Path.Combine(_arguments.OutputDirectory, "driver.log");

            // We want to explicitly specify a timeout here. This is for for the
            // driver commands, like getLog. The default is 60s, which ends up
            // timing out when getLog() is waiting, and doesn't receive anything
            // for 60s.
            //
            // Since, we almost all the output gets written via the websocket now,
            // getLog() might not see anything for long durations!
            //
            // So -> use a larger timeout!

            int max_retries = 3;
            int retry_num = 0;
            while(true)
            {
                try
                {
                    return (driverService, new ChromeDriver(driverService, options, _arguments.Timeout));
                }
                catch (WebDriverException wde) when (wde.Message.Contains("exited abnormally") && retry_num < max_retries - 1)
                {
                    // chrome can sometimes crash on startup when launching from chromedriver.
                    // As a *workaround*, let's retry that a few times
                    // Error seen:
                    //     [12:41:07] crit: OpenQA.Selenium.WebDriverException: unknown error: Chrome failed to start: exited abnormally.
                    //    (chrome not reachable)

                    // Log on max-1 tries, and rethrow on the last one
                    logger.LogWarning($"Failed to start chrome, attempt #{retry_num}: {wde}");
                }

                retry_num++;
            }
        }
    }
}
