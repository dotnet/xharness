// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
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

        diagnosticsData.Target = Arguments.Browser.Value.ToString();
        (IBrowser? browser, IBrowserContext? context) = Arguments.Browser.Value switch
        {
            Browser.Chrome => await GetChromiumBrowserAsync(logger, "chrome", Arguments.Locale),
            // Browser.Safari => await GetSafariBrowserAsync(logger), // ToDo: fix compilation error
            Browser.Firefox => await GetFirefoxBrowserAsync(logger),
            Browser.Edge => await GetChromiumBrowserAsync(logger, "msedge", Arguments.Locale),

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
            var runner = new WasmBrowserTestRunner(
                            Arguments,
                            PassThroughArguments,
                            logProcessor,
                            logger);
            var exitCode = await runner.RunTestsWithPlaywright(browser, context);

            if ((int)exitCode != Arguments.ExpectedExitCode)
            {
                logger.LogError($"Application has finished with exit code {exitCode} but {Arguments.ExpectedExitCode} was expected");
                return ExitCode.GENERAL_FAILURE;
            }
            if (logProcessor.LineThatMatchedErrorPattern != null)
            {
                logger.LogError($@"Application exited with the expected exit code: {exitCode}.
                                But found a line matching an error pattern: {logProcessor.LineThatMatchedErrorPattern}");
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

            try
            {
                if (browser != null)
                {
                    await browser.CloseAsync();
                    await browser.DisposeAsync();
                    browser = null;
                }
            }
            catch (Exception e)
            {
                logger.LogError($"Error while closing browser: {e}");
            }
        }
    }

    private BrowserTypeLaunchOptions GetLaunchOptions(IEnumerable<string> args, ILogger logger)
    {
        var launchOptions = new BrowserTypeLaunchOptions
        {
            Headless = !Arguments.NoHeadless && !Arguments.BackgroundThrottling,
            Args = args
        };

        if (!string.IsNullOrEmpty(Arguments.BrowserLocation))
        {
            launchOptions.ExecutablePath = Arguments.BrowserLocation;
            logger.LogInformation($"Using browser from {Arguments.BrowserLocation}");
        }

        return launchOptions;
    }

    private BrowserTypeLaunchOptions GetChromiumLaunchOptions(ILogger logger, IEnumerable<string> browserArgs, string channel)
    {
        var args = new List<string>(browserArgs);
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

        var launchOptions = GetLaunchOptions(args, logger);

        launchOptions.Channel = channel;
        return launchOptions;
    }

    // cannot find WebKit. WHY? docs use same logic:
    // https://github.com/microsoft/playwright/blob/3c5967d4f56139a3f1bdd2837a896ed90d9b33de/docs/src/api/class-playwright.md?plain=1#L169
    // 'IPlaywright' does not contain a definition for 'WebKit' and no accessible extension method 'WebKit' accepting a first argument of type 'IPlaywright' could be found
    // private async Task<(IBrowser, IBrowserContext?)> GetSafariBrowserAsync(ILogger logger)
    // {
    //     var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
    //     var arguments = Arguments.BrowserArgs.Value;
    //     var launchOptions = GetLaunchOptions(arguments, logger);

    //     logger.LogInformation($"Starting Safari with args: {string.Join(' ', arguments)}");
    //     var browser = await playwright.WebKit.LaunchAsync(launchOptions);

    //     // new context is an equivalent of incognito mode
    //     if (Arguments.NoIncognito)
    //     {
    //         return (browser, null);
    //     }
    //     var contextOptions = new BrowserNewContextOptions();
    //     var context = await browser.NewContextAsync(contextOptions);
    //     return (browser, context);
    // }

    private async Task<(IBrowser, IBrowserContext?)> GetFirefoxBrowserAsync(ILogger logger)
    {
        var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        var arguments = Arguments.BrowserArgs.Value;
        var launchOptions = GetLaunchOptions(arguments, logger);

        logger.LogInformation($"Starting Firefox with args: {string.Join(' ', arguments)}");
        var browser = await playwright.Firefox.LaunchAsync(launchOptions);

        // new context is an equivalent of incognito mode
        if (Arguments.NoIncognito)
        {
            return (browser, null);
        }
        var contextOptions = new BrowserNewContextOptions();
        var context = await browser.NewContextAsync(contextOptions);
        return (browser, context);
    }

    private async Task<(IBrowser, IBrowserContext?)> GetChromiumBrowserAsync(ILogger logger, string channel, string sessionLanguage)
    {
        var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        var launchOptions = GetChromiumLaunchOptions(logger, Arguments.BrowserArgs.Value, channel);

        foreach (var file in Directory.EnumerateFiles(Arguments.OutputDirectory, "chromedriver-*.log"))
            File.Delete(file);

        logger.LogInformation($"Starting chromium with args: {string.Join(' ', Arguments.BrowserArgs.Value)}");

        int max_retries = 3;
        for (int retry_num = 0; retry_num < max_retries; retry_num++)
        {
            try
            {
                logger.LogInformation($"Attempt #{retry_num} out of {max_retries}");
                var browser = await playwright.Chromium.LaunchAsync(launchOptions);

                var headers = new Dictionary<string, string> {
                    { "Accept-Language", sessionLanguage }
                };

                // new context is an equivalent of incognito mode
                if (!Arguments.NoIncognito)
                {
                    var incognitoContext = await browser.NewContextAsync(new() {
                        // Locale = sessionLanguage,
                        ExtraHTTPHeaders = headers
                    });
                    return (browser, incognitoContext);
                }
                
                var context = browser.Contexts.FirstOrDefault();
                if (context == null)
                {
                    logger.LogWarning("No default browser context available. To set the session language a new context will be created and incognito mode will be used");
                    context = await browser.NewContextAsync(new () {
                        ExtraHTTPHeaders = headers
                    });
                }
                await context.SetExtraHTTPHeadersAsync(headers);
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
