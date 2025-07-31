// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Wasm;

internal class WasmTestBrowserCommand : XHarnessCommand<WasmTestBrowserCommandArguments>
{
    private const string CommandHelp = "Executes tests on WASM using a browser";

    protected override string CommandUsage { get; } = "wasm test-browser [OPTIONS] -- [BROWSER OPTIONS]";
    protected override string CommandDescription { get; } = CommandHelp;

    protected override WasmTestBrowserCommandArguments Arguments { get; } = new();

    public WasmTestBrowserCommand()
        : base(TargetPlatform.WASM, "test-browser", allowsExtraArgs: true, new ServiceCollection(), CommandHelp)
    {
    }

    protected override async Task<ExitCode> InvokeInternal(ILogger logger)
    {
        var xmlResultsFilePath = Path.Combine(Arguments.OutputDirectory, "testResults.xml");
        File.Delete(xmlResultsFilePath);

        var stdoutFilePath = Path.Combine(Arguments.OutputDirectory, "wasm-console.log");
        File.Delete(stdoutFilePath);

        var symbolicator = WasmSymbolicatorBase.Create(Arguments.SymbolicatorArgument.GetLoadedTypes().FirstOrDefault(),
                                                       Arguments.SymbolMapFileArgument,
                                                       Arguments.SymbolicatePatternsFileArgument,
                                                       logger);

        var serviceProvider = Services.BuildServiceProvider();
        var diagnosticsData = serviceProvider.GetRequiredService<IDiagnosticsData>();

        var logProcessor = new WasmTestMessagesProcessor(xmlResultsFilePath,
                                                         stdoutFilePath,
                                                         logger,
                                                         Arguments.ErrorPatternsFile,
                                                         symbolicator);
        var runner = new WasmBrowserTestRunner(
                            Arguments,
                            PassThroughArguments,
                            logProcessor,
                            logger);

        diagnosticsData.Target = Arguments.Browser.Value.ToString();
        (PlaywrightServiceWrapper driverService, PlaywrightBrowserWrapper driver) = Arguments.Browser.Value switch
        {
            Browser.Chrome => await GetChromeDriverAsync(Arguments.Locale, logger),
            Browser.Safari => await GetSafariDriverAsync(logger),
            Browser.Firefox => await GetFirefoxDriverAsync(logger),
            Browser.Edge => await GetEdgeDriverAsync(Arguments.Locale, logger),

            // shouldn't reach here
            _ => throw new ArgumentException($"Unknown browser : {Arguments.Browser}")
        };

        try
        {
            var exitCode = await runner.RunTestsWithPlaywrightAsync(driverService, driver);
            if ((int)exitCode != Arguments.ExpectedExitCode)
            {
                logger.LogError($"Application has finished with exit code {exitCode} but {Arguments.ExpectedExitCode} was expected");
                return ExitCode.GENERAL_FAILURE;
            }

            if (logProcessor.LineThatMatchedErrorPattern != null)
            {
                logger.LogError("Application exited with the expected exit code: {exitCode}."
                                + $" But found a line matching an error pattern: {logProcessor.LineThatMatchedErrorPattern}");
                return ExitCode.APP_CRASH;
            }

            return ExitCode.SUCCESS;
        }
        finally
        {
            if (Arguments.NoQuit)
            {
                logger.LogInformation("Tests are done. Press Ctrl+C to exit");
                var token = new CancellationToken(false);
                token.WaitHandle.WaitOne();
            }

            // Playwright handles browser cleanup automatically, but we still do graceful shutdown
            var cts = new CancellationTokenSource();
            cts.CancelAfter(10000);
            try
            {
                logger.LogInformation($"Closing browser gracefully.");
                if (driverService.IsRunning)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cts.Token);
                    driver.Dispose(); // This will close page, browser, and playwright
                    driverService.Dispose();
                }
            }
            catch (Exception e)
            {
                logger.LogError($"Error while closing browser: {e}");
            }
        }
    }

    private async Task<(PlaywrightServiceWrapper, PlaywrightBrowserWrapper)> GetSafariDriverAsync(ILogger logger)
    {
        logger.LogInformation("Starting Safari");

        var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        var browser = await playwright.Webkit.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = !Arguments.NoHeadless,
            Args = Arguments.BrowserArgs.Value.ToList(),
            Timeout = (float?)Arguments.Timeout.Value.TotalMilliseconds,
            ExecutablePath = !string.IsNullOrEmpty(Arguments.BrowserLocation) ? Arguments.BrowserLocation : null
        });

        var page = await browser.NewPageAsync();
        
        // Setup console logging to match Selenium behavior
        page.Console += (_, e) => logger.LogDebug($"[Browser Console] {e.Text}");

        return (new PlaywrightServiceWrapper(browser), new PlaywrightBrowserWrapper(page, browser, playwright));
    }

    private async Task<(PlaywrightServiceWrapper, PlaywrightBrowserWrapper)> GetFirefoxDriverAsync(ILogger logger)
    {
        if (!string.IsNullOrEmpty(Arguments.BrowserLocation))
        {
            logger.LogInformation($"Using Firefox from {Arguments.BrowserLocation}");
        }

        var args = Arguments.BrowserArgs.Value.ToList();
        if (!Arguments.NoHeadless)
            args.Add("--headless");

        if (!Arguments.NoIncognito)
            args.Add("-private-window");

        logger.LogInformation($"Starting Firefox with args: {string.Join(' ', args)}");

        var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        var browser = await playwright.Firefox.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = !Arguments.NoHeadless,
            Args = args,
            Timeout = (float?)Arguments.Timeout.Value.TotalMilliseconds,
            ExecutablePath = !string.IsNullOrEmpty(Arguments.BrowserLocation) ? Arguments.BrowserLocation : null
        });

        var page = await browser.NewPageAsync();
        
        // Setup console logging
        page.Console += (_, e) => logger.LogDebug($"[Browser Console] {e.Text}");

        return (new PlaywrightServiceWrapper(browser), new PlaywrightBrowserWrapper(page, browser, playwright));
    }

    private async Task<(PlaywrightServiceWrapper, PlaywrightBrowserWrapper)> GetChromeDriverAsync(string sessionLanguage, ILogger logger)
        => await GetChromiumDriverAsync<BrowserTypeLaunchOptions>(
                    "chromedriver",
                    sessionLanguage,
                    (playwright) => playwright.Chromium,
                    logger);

    private async Task<(PlaywrightServiceWrapper, PlaywrightBrowserWrapper)> GetEdgeDriverAsync(string sessionLanguage, ILogger logger)
        => await GetChromiumDriverAsync<BrowserTypeLaunchOptions>(
                    "edgedriver",
                    sessionLanguage,
                    (playwright) => playwright.Chromium, // Edge uses Chromium engine
                    logger);

    private async Task<(PlaywrightServiceWrapper, PlaywrightBrowserWrapper)> GetChromiumDriverAsync<TDriverOptions>(
        string driverName, string sessionLanguage, Func<IPlaywright, IBrowserType> getBrowserType, ILogger logger)
        where TDriverOptions : BrowserTypeLaunchOptions, new()
    {
        var args = Arguments.BrowserArgs.Value.ToList();

        if (!Arguments.NoHeadless && !Arguments.BackgroundThrottling)
            args.Add("--headless");

        if (Arguments.DebuggerPort.Value != null)
            args.Add($"--remote-debugging-port={Arguments.DebuggerPort}");

        if (!Arguments.NoIncognito)
            args.Add("--incognito");

        if (!Arguments.BackgroundThrottling)
        {
            args.AddRange(new[]
            {
                "--disable-background-timer-throttling",
                "--disable-backgrounding-occluded-windows",
                "--disable-renderer-backgrounding",
                "--enable-features=NetworkService,NetworkServiceInProcess",
            });
        }
        else
        {
            args.Add(@"--enable-features=IntensiveWakeUpThrottling:grace_period_seconds/1");
        }

        args.AddRange(new[]
        {
            // added based on https://github.com/puppeteer/puppeteer/blob/main/src/node/Launcher.ts#L159-L181
            "--allow-insecure-localhost",
            "--disable-breakpad",
            "--disable-component-extensions-with-background-pages",
            "--disable-dev-shm-usage",
            "--disable-extensions",
            "--disable-features=TranslateUI",
            "--disable-ipc-flooding-protection",
            "--force-color-profile=srgb",
            "--metrics-recording-only"
        });

        if (File.Exists("/.dockerenv"))
        {
            // Use --no-sandbox for containers, and codespaces
            args.Add("--no-sandbox");
        }

        logger.LogInformation($"Starting {driverName} with args: {string.Join(' ', args)}");

        // Retry logic preserved from original Selenium implementation
        string[] err_snippets = new[]
        {
            "exited abnormally",
            "Cannot start the driver service",
            "failed to start"
        };

        int max_retries = 3;
        int retry_num = 0;
        while (true)
        {
            IPlaywright? playwright = null;
            IBrowser? browser = null;
            try
            {
                playwright = await Microsoft.Playwright.Playwright.CreateAsync();
                var browserType = getBrowserType(playwright);
                
                // Set environment for browser launch
                var env = new System.Collections.Generic.Dictionary<string, string>();
                if (!string.IsNullOrEmpty(sessionLanguage))
                {
                    env["LANGUAGE"] = sessionLanguage;
                }

                // Determine channel for branded browsers
                string? channel = null;
                if (driverName == "edgedriver")
                    channel = "msedge";
                else if (driverName == "chromedriver" && !string.IsNullOrEmpty(Arguments.BrowserLocation))
                    channel = "chrome";

                browser = await browserType.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = !Arguments.NoHeadless,
                    Args = args,
                    Timeout = (float?)Arguments.Timeout.Value.TotalMilliseconds,
                    ExecutablePath = !string.IsNullOrEmpty(Arguments.BrowserLocation) ? Arguments.BrowserLocation : null,
                    Channel = channel,
                    Env = env.Count > 0 ? env : null
                });

                var page = await browser.NewPageAsync();
                
                // Setup console logging
                page.Console += (_, e) => logger.LogDebug($"[Browser Console] {e.Text}");

                return (new PlaywrightServiceWrapper(browser), new PlaywrightBrowserWrapper(page, browser, playwright));
            }
            catch (Exception ex) when (err_snippets.Any(s => ex.ToString().Contains(s)) && retry_num < max_retries - 1)
            {
                // Preserve retry logic from Selenium implementation
                logger.LogWarning($"Failed to start the browser, attempt #{retry_num}: {ex}");

                browser?.CloseAsync().Wait();
                playwright?.Dispose();
            }
            catch
            {
                browser?.CloseAsync().Wait();
                playwright?.Dispose();
                throw;
            }

            retry_num++;
        }
    }
}
