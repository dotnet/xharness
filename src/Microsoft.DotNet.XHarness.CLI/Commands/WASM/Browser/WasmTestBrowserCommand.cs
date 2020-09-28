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
                                logProcessor.Invoke,
                                logger);

            var (driverService, driver) = GetChromeDriver();
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

        private (DriverService, IWebDriver) GetChromeDriver()
        {
            var options = new ChromeOptions();
            options.SetLoggingPreference(LogType.Browser, SeleniumLogLevel.All);

            // Enable this for more debugging info
            // options.SetLoggingPreference(LogType.Driver, SeleniumLogLevel.All);

            options.AddArguments(new List<string>(_arguments.BrowserArgs)
            {
                "--incognito",
                "--headless"
            });

            var driverService = ChromeDriverService.CreateDefaultService();

            // We want to explicitly specify a timeout here. This is for for the
            // driver commands, like getLog. The default is 60s, which ends up
            // timing out when getLog() is waiting, and doesn't receive anything
            // for 60s.
            //
            // Since, we almost all the output gets written via the websocket now,
            // getLog() might not see anything for long durations!
            //
            // So -> use a larger timeout!
            return (driverService, new ChromeDriver(driverService, options, _arguments.Timeout));
        }
    }
}
