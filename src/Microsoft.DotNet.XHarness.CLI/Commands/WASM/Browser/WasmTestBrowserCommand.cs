// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
using OpenQA.Selenium.Safari;
using OpenQA.Selenium.Firefox;
using SeleniumLogLevel = OpenQA.Selenium.LogLevel;
using OpenQA.Selenium;
using System.Linq;
using OpenQA.Selenium.Edge;
using System.Runtime.InteropServices;
using OpenQA.Selenium.Chromium;

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

            (DriverService driverService, IWebDriver driver) = _arguments.Browser switch
            {
                Browser.Chrome => GetChromeDriver(logger),
                Browser.Safari => GetSafariDriver(logger),
                Browser.Firefox => GetFirefoxDriver(logger),
                Browser.Edge => GetEdgeDriver(logger),

                // shouldn't reach here
                _              => throw new ArgumentException($"Unknown browser : {_arguments.Browser}")
            };

            try
            {
                var exitCode = await runner.RunTestsWithWebDriver(driverService, driver);
                if ((int)exitCode != _arguments.ExpectedExitCode)
                {
                    logger.LogError($"Application has finished with exit code {exitCode} but {_arguments.ExpectedExitCode} was expected");
                    return ExitCode.GENERAL_FAILURE;
                }

                return ExitCode.SUCCESS;
            }
            finally
            {
                driver.Quit();  // Firefox driver hangs if Quit is not issued.
                driverService.Dispose();
                driver.Dispose();
            }
        }

        private (DriverService, IWebDriver) GetSafariDriver(ILogger logger)
        {
            var options = new SafariOptions();
            options.SetLoggingPreference(LogType.Browser, SeleniumLogLevel.All);

            logger.LogInformation("Starting Safari");

            return CreateWebDriver(
                        () => SafariDriverService.CreateDefaultService(),
                        driverService => new SafariDriver(driverService, options, _arguments.Timeout));
        }

        private (DriverService, IWebDriver) GetFirefoxDriver(ILogger logger)
        {
            var options = new FirefoxOptions();
            options.SetLoggingPreference(LogType.Browser, SeleniumLogLevel.All);

            options.AddArguments(new List<string>(_arguments.BrowserArgs)
            {
                "--incognito",
                "--headless",
            });

            logger.LogInformation($"Starting Firefox with args: {string.Join(' ', options.ToCapabilities())}");

            return CreateWebDriver(
                        () => FirefoxDriverService.CreateDefaultService(),
                        (driverService) => new FirefoxDriver(driverService, options, _arguments.Timeout));
        }

        private (DriverService, IWebDriver) GetChromeDriver(ILogger logger)
            => GetChromiumDriver<ChromeOptions, ChromeDriver, ChromeDriverService>(
                        "chromedriver",
                        options => ChromeDriverService.CreateDefaultService(),
                        logger);

        private (DriverService, IWebDriver) GetEdgeDriver(ILogger logger)
            => GetChromiumDriver<EdgeOptions, EdgeDriver, EdgeDriverService>(
                        "edgedriver",
                        options =>
                        {
                            options.UseChromium = true;
                            return EdgeDriverService.CreateDefaultServiceFromOptions(options);
                        }, logger);

        private (DriverService, IWebDriver) GetChromiumDriver<TDriverOptions, TDriver, TDriverService>(
            string driverName, Func<TDriverOptions, TDriverService> getDriverService, ILogger logger)
            where TDriver: ChromiumDriver
            where TDriverOptions: ChromiumOptions
            where TDriverService: ChromiumDriverService
        {
            var options = Activator.CreateInstance<TDriverOptions>();
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

            logger.LogInformation($"Starting {driverName} with args: {string.Join(' ', options.Arguments)}");

            // We want to explicitly specify a timeout here. This is for for the
            // driver commands, like getLog. The default is 60s, which ends up
            // timing out when getLog() is waiting, and doesn't receive anything
            // for 60s.
            //
            // Since, we almost all the output gets written via the websocket now,
            // getLog() might not see anything for long durations!
            //
            // So -> use a larger timeout!

            string[] err_snippets = new []
            {
                "exited abnormally",
                "Cannot start the driver service"
            };

            foreach (var file in Directory.EnumerateFiles(_arguments.OutputDirectory, $"{driverName}-*.log"))
                File.Delete(file);

            int max_retries = 3;
            int retry_num = 0;
            while(true)
            {
                TDriverService? driverService = null;
                try
                {
                    driverService = getDriverService(options);

                    driverService.EnableAppendLog = false;
                    driverService.EnableVerboseLogging = true;
                    driverService.LogPath = Path.Combine(_arguments.OutputDirectory, $"{driverName}-{retry_num}.log");

                    if (!(Activator.CreateInstance(typeof(TDriver), driverService, options, _arguments.Timeout) is TDriver driver))
                        throw new ArgumentException($"Failed to create instance of {typeof(TDriver)}");

                    return (driverService, driver);
                }
                catch (WebDriverException wde) when (err_snippets.Any(s => wde.Message.Contains(s)) && retry_num < max_retries - 1)
                {
                    // chrome can sometimes crash on startup when launching from chromedriver.
                    // As a *workaround*, let's retry that a few times
                    // Example error seen:
                    //     [12:41:07] crit: OpenQA.Selenium.WebDriverException: unknown error: Chrome failed to start: exited abnormally.
                    //    (chrome not reachable)

                    // Log on max-1 tries, and rethrow on the last one
                    logger.LogWarning($"Failed to start the browser, attempt #{retry_num}: {wde}");

                    driverService?.Dispose();
                }
                catch
                {
                    driverService?.Dispose();
                    throw;
                }

                retry_num++;
            }
        }

        private static (DriverService, IWebDriver) CreateWebDriver<TDriverService>(Func<TDriverService> getDriverService, Func<TDriverService, IWebDriver> getDriver)
            where TDriverService: DriverService
        {
            var driverService = getDriverService();
            try
            {
                return (driverService, getDriver(driverService));
            }
            catch
            {
                driverService?.Dispose();
                throw;
            }
        }
    }
}
