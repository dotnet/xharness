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
using Microsft.Playwright;

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
        (IBrowser browser, IBrowserContext context) = Arguments.Browser.Value switch
        {
            Browser.Chrome => await GetChromiumBrowserAsync(logger, "chrome"),
            Browser.Safari => await GetSafariBrowserAsync(logger),
            Browser.Firefox => await GetFirefoxBrowserAsync(logger),
            Browser.Edge => await GetChromiumBrowserAsync(logger, "msedge"),

            // shouldn't reach here
            _ => throw new ArgumentException($"Unknown browser : {Arguments.Browser}")
        };
        try
        {
            browser.Disconnected += (sender, e) =>
            {
                browser = null;
                logger.LogWarning("Browser has been disconnected");
            };
            var page = await browser.NewPageAsync();

            // Log console messages
            page.Console += (_, msg) => { logger.LogInformation($"Console message: {msg}"); };
            page.PageError += (_, msg) => { logger.LogError($"Page error: {msg}"); };
            page.FrameDetached += (_, msg) => { logger.LogError($"Frame detached: {msg}"); };

            string testUrl = runner.GetTestUrl(); // HERE we have to start the server most probably
            logger.LogDebug($"Opening in browser: {testUrl}");
        
            await page.GotoAsync(testUrl);
            await WaitForPageLoadStateAsync(page, Arguments.PageLoadStrategy.Value);

            // ToDo: these codes might be useful for the server
            //     logger.LogError($"Application has finished with exit code {exitCode} but {Arguments.ExpectedExitCode} was expected");
            //     return ExitCode.GENERAL_FAILURE;
            // if (logProcessor.LineThatMatchedErrorPattern != null)
            // {
            //     logger.LogError("Application exited with the expected exit code: {exitCode}."
            //                     + $" But found a line matching an error pattern: {logProcessor.LineThatMatchedErrorPattern}");
            //     return ExitCode.APP_CRASH;
            // }
            // return ExitCode.SUCCESS;
        }
        finally
        {
            if (Arguments.NoQuit)
            {
                logger.LogInformation("Tests are done. Press Ctrl+C to exit");
                var token = new CancellationToken(false);
                token.WaitHandle.WaitOne();
            }

            // close all tabs before quit is a workaround for broken Selenium - GeckoDriver communication in Firefox
            // https://github.com/dotnet/runtime/issues/101617
            // most probably it will not be needed with playwright but we will keep an equivalent of it for now
            var cts = new CancellationTokenSource();
            cts.CancelAfter(10000);
            try
            {
                await CloseAllTabs(context, cts.Token);
                await browser.CloseAsync();
                if (browser is not null)
                {
                    await Browser.DisposeAsync();
                    Browser = null;
                }
            }
            catch (Exception e)
            {
                logger.LogError($"Error while closing browser: {e}");
            }
        }
    }

    private async Task CloseAllTabs(IBrowserContext context, CancellationToken cancellationToken
    {
        var pages = context.Pages;
        logger.LogInformation($"Closing {pages.Count} browser tabs before quitting.");
        foreach (var page in pages)
        {
            if (cts.IsCancellationRequested)
            {
                logger.LogInformation($"Timeout while trying to close tabs, {context.Pages.Count} is left open before quitting.");
                break;
            }
            await page.CloseAsync();
        }
    }

    private async Task WaitForPageLoadStateAsync(IPage page, string pageLoadStrategy)
    {
        // Translation of selenium PageLoadStrategy
        // If pageLoadStrategy is "none", do not wait for any load state
        int timeout = 1 * 60 * 1000;
        if (pageLoadStrategy == "normal")
        {
            await page.WaitForLoadStateAsync(LoadState.Load, new () { Timeout = timeout });
        }
        else if (pageLoadStrategy == "eager")
        {
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new () { Timeout = timeout });
        }
    }

    private BrowserTypeLaunchOptions GetLaunchOptions(ILogger logger, IEnumerable<string> args)
    {
        var launchOptions = new BrowserTypeLaunchOptions
        {
            Headless = !Arguments.NoHeadless && !Arguments.BackgroundThrottling,
            Args = args,
            Logger = new PlaywrightLogger(LogLevel.Trace)
        };

        if (!string.IsNullOrEmpty(Arguments.BrowserLocation))
        {
            launchOptions.ExecutablePath = Arguments.BrowserLocation;
            logger.LogInformation($"Using browser from {Arguments.BrowserLocation}");
        }

        return launchOptions;
    }

    private BrowserTypeLaunchOptions GetChromiumLaunchOptions(ILogger logger, IEnumerable<string> args, string channel)
    {
        var args = new List<string>(args);
        args.AddRange(new[]
        {
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

        if (Arguments.DebuggerPort.Value != null)
        {
            args.Add($"--remote-debugging-port={Arguments.DebuggerPort}");
        }

        if (!Arguments.BackgroundThrottling)
        {
            args.AddRange(new[]
            {
                "--disable-background-timer-throttling",
                "--disable-backgrounding-occluded-windows",
                "--disable-renderer-backgrounding",
                "--enable-features=NetworkService,NetworkServiceInProcess"
            });
        }
        else
        {
            args.Add("--enable-features=IntensiveWakeUpThrottling:grace_period_seconds/1");
        }

        if (File.Exists("/.dockerenv"))
        {
            args.Add("--no-sandbox");
        }

        var launchOptions = GetLaunchOptions(logger, args);

        launchOptions.Channel = channel;
        return launchOptions;
    }

    private async Task<(IBrowser, IBrowserContext)> GetSafariBrowserAsync(ILogger logger)
    {
        var playwright = await Playwright.CreateAsync();
        var arguments = Arguments.BrowserArgs.Value;
        var launchOptions = GetLaunchOptions(logger, arguments);

        logger.LogInformation($"Starting Safari with args: {string.Join(' ', arguments)}");
        var browser = await playwright.WebKit.LaunchAsync(launchOptions);

        // Safari does not support IsIncognito option
        var contextOptions = new BrowserNewContextOptions();
        var context = await browser.NewContextAsync(contextOptions);
        return (browser, context);
    }

    private async Task<(IBrowser, IBrowserContext)> GetFirefoxBrowserAsync(ILogger logger)
    {
        var playwright = await Playwright.CreateAsync();
        var arguments = Arguments.BrowserArgs.Value;
        var launchOptions = GetLaunchOptions(logger, arguments);

        logger.LogInformation($"Starting Firefox with args: {string.Join(' ', arguments)} and load strategy: {Arguments.PageLoadStrategy.Value}");
        var browser = await playwright.Firefox.LaunchAsync(launchOptions);

        var contextOptions = new BrowserNewContextOptions();
        if (!Arguments.NoIncognito)
        {
            contextOptions.IsIncognito = true;
        }
        var context = await browser.NewContextAsync(contextOptions);
        return (browser, context);
    }

    private async Task<(IBrowser, IBrowserContext)> GetChromiumBrowserAsync(ILogger logger, string channel)
    {
        var playwright = await Playwright.CreateAsync();
        var launchOptions = GetChromiumLaunchOptions(logger, Arguments.BrowserArgs.Value, channel);

        foreach (var file in Directory.EnumerateFiles(Arguments.OutputDirectory, "chromedriver-*.log"))
            File.Delete(file);

        logger.LogInformation($"Starting chromium with args: {string.Join(' ', Arguments.BrowserArgs.Value)} and load strategy: {Arguments.PageLoadStrategy.Value}");

        int max_retries = 3;
        for (int retry_num = 0; retry_num < max_retries; retry_num++)
        {
            try
            {
                logger.LogInformation($"Attempt #{retry_num} out of {max_retries}");
                var browser = await playwright.Chromium.LaunchAsync(launchOptions);
                var contextOptions = new BrowserNewContextOptions(
                    Locale = sessionLanguage
                );
                if (!Arguments.NoIncognito)
                {
                    contextOptions.IsIncognito = true;
                }
                var context = await browser.NewContextAsync(contextOptions);
                return (browser, context);
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to start the browser, attempt #{retry_num}: {ex}");
            }
        }
        throw new InvalidOperationException("Failed to start the browser");
    }
}
