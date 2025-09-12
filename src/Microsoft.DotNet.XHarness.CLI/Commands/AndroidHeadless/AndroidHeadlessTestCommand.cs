// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.XHarness.Android;
using Microsoft.DotNet.XHarness.CLI.Android;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.AndroidHeadless;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.AndroidHeadless;

internal class AndroidHeadlessTestCommand : AndroidCommand<AndroidHeadlessTestCommandArguments>
{
    protected override AndroidHeadlessTestCommandArguments Arguments { get; } = new();

    protected override string CommandUsage { get; } = "android-headless test --output-directory=... --test-folder=... --test-command=... [OPTIONS]";

    private const string CommandHelp = "Executes test executable on an Android device, waits up to a given timeout, then copies files off the device and uninstalls the test app";
    protected override string CommandDescription { get; } = @$"
{CommandHelp}
 
Arguments:
";

    public AndroidHeadlessTestCommand() : base("test", false, CommandHelp)
    {
    }

    protected override ExitCode InvokeCommand(ILogger logger)
    {
        if (!Directory.Exists(Arguments.TestPath))
        {
            logger.LogCritical($"Couldn't find test {Arguments.TestPath}!");
            return ExitCode.PACKAGE_NOT_FOUND;
        }
        if (!Directory.Exists(Arguments.RuntimePath))
        {
            logger.LogCritical($"Couldn't find shared runtime {Arguments.RuntimePath}!");
            return ExitCode.PACKAGE_NOT_FOUND;
        }

        IEnumerable<string> testRequiredArchitecture = Arguments.DeviceArchitecture.Value;
        logger.LogInformation($"Required architecture: '{string.Join("', '", testRequiredArchitecture)}'");

        var runner = new AdbRunner(logger);

        // Determine which API levels to test
        var apiLevelsToTest = new List<int>();
        
        if (Arguments.ApiLevels.Value.Any())
        {
            // Use multiple API levels if specified
            apiLevelsToTest.AddRange(Arguments.ApiLevels.ApiLevels);
            logger.LogInformation($"Running tests on API levels: {string.Join(", ", apiLevelsToTest)}");
        }
        else if (Arguments.ApiVersion.Value.HasValue)
        {
            // Use single API version if specified
            apiLevelsToTest.Add(Arguments.ApiVersion.Value.Value);
            logger.LogInformation($"Running tests on API level: {Arguments.ApiVersion.Value}");
        }
        else
        {
            // No specific API level - use any available device
            apiLevelsToTest.Add(0); // 0 means any API level
            logger.LogInformation("Running tests on any available device");
        }

        ExitCode finalExitCode = ExitCode.SUCCESS;
        var failedApiLevels = new List<int>();

        // Run tests on each specified API level
        foreach (var apiLevel in apiLevelsToTest)
        {
            logger.LogInformation($"=== Testing on API level {(apiLevel == 0 ? "any" : apiLevel.ToString())} ===");
            
            var currentApiLevel = apiLevel == 0 ? (int?)null : apiLevel;
            
            // Start emulator if needed and API level is specified
            if (currentApiLevel.HasValue)
            {
                if (!runner.StartEmulator(currentApiLevel.Value))
                {
                    logger.LogError($"Failed to start emulator for API level {currentApiLevel}");
                    failedApiLevels.Add(currentApiLevel.Value);
                    finalExitCode = ExitCode.DEVICE_NOT_FOUND;
                    continue;
                }
            }

            var exitCode = AndroidHeadlessInstallCommand.InvokeHelper(
                logger: logger,
                testPath: Arguments.TestPath,
                runtimePath: Arguments.RuntimePath,
                testRequiredArchitecture: testRequiredArchitecture,
                deviceId: Arguments.DeviceId.Value,
                apiVersion: currentApiLevel,
                bootTimeoutSeconds: Arguments.LaunchTimeout,
                runner,
                DiagnosticsData);

            if (exitCode == ExitCode.SUCCESS)
            {
                // Create API-level specific output directory if testing multiple levels
                var outputDirectory = Arguments.OutputDirectory.Value;
                if (apiLevelsToTest.Count > 1 && currentApiLevel.HasValue)
                {
                    outputDirectory = Path.Combine(Arguments.OutputDirectory.Value, $"api-{currentApiLevel}");
                    Directory.CreateDirectory(outputDirectory);
                    logger.LogInformation($"Using API-specific output directory: {outputDirectory}");
                }

                exitCode = AndroidHeadlessRunCommand.InvokeHelper(
                    logger: logger,
                    testPath: Arguments.TestPath,
                    runtimePath: Arguments.RuntimePath,
                    testAssembly: Arguments.TestAssembly,
                    testScript: Arguments.TestScript,
                    outputDirectory: outputDirectory,
                    timeout: Arguments.Timeout,
                    expectedExitCode: Arguments.ExpectedExitCode,
                    wifi: Arguments.Wifi,
                    runner: runner);
            }

            if (exitCode != ExitCode.SUCCESS)
            {
                if (currentApiLevel.HasValue)
                {
                    failedApiLevels.Add(currentApiLevel.Value);
                }
                
                if (finalExitCode == ExitCode.SUCCESS)
                {
                    finalExitCode = exitCode;
                }
                
                logger.LogError($"Tests failed on API level {(currentApiLevel?.ToString() ?? "any")} with exit code: {exitCode}");
            }
            else
            {
                logger.LogInformation($"Tests passed on API level {(currentApiLevel?.ToString() ?? "any")}");
            }
        }

        // Cleanup
        runner.DeleteHeadlessFolder(Arguments.TestPath);
        runner.DeleteHeadlessFolder("runtime");

        // Stop emulators if we started them (only if user specified API levels)
        if (Arguments.ApiLevels.Value.Any())
        {
            runner.StopEmulators(Arguments.ApiLevels.ApiLevels);
        }

        // Report summary
        if (apiLevelsToTest.Count > 1)
        {
            logger.LogInformation($"=== Test Summary ===");
            logger.LogInformation($"Total API levels tested: {apiLevelsToTest.Count}");
            logger.LogInformation($"Successful: {apiLevelsToTest.Count - failedApiLevels.Count}");
            
            if (failedApiLevels.Any())
            {
                logger.LogError($"Failed API levels: {string.Join(", ", failedApiLevels)}");
            }
        }

        return finalExitCode;
    }
}
