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

        // Determine which API versions to test
        var apiVersionsToTest = new List<int>();
        
        if (Arguments.ApiVersion.Value.Any())
        {
            // Use multiple API versions if specified
            apiVersionsToTest.AddRange(Arguments.ApiVersion.ApiVersions);
            logger.LogInformation($"Running tests on API versions: {string.Join(", ", apiVersionsToTest)}");
        }
        else
        {
            // No specific API version - use any available device
            apiVersionsToTest.Add(0); // 0 means any API version
            logger.LogInformation("Running tests on any available device");
        }

        ExitCode finalExitCode = ExitCode.SUCCESS;
        var failedApiVersions = new List<int>();

        // Run tests on each specified API version
        foreach (var apiVersion in apiVersionsToTest)
        {
            logger.LogInformation($"=== Testing on API version {(apiVersion == 0 ? "any" : apiVersion.ToString())} ===");
            
            var currentApiVersion = apiVersion == 0 ? (int?)null : apiVersion;
            
            // Start emulator if needed and API version is specified
            if (currentApiVersion.HasValue)
            {
                if (!runner.StartEmulator(currentApiVersion.Value))
                {
                    logger.LogError($"Failed to start emulator for API version {currentApiVersion}");
                    failedApiVersions.Add(currentApiVersion.Value);
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
                apiVersion: currentApiVersion,
                bootTimeoutSeconds: Arguments.LaunchTimeout,
                runner,
                DiagnosticsData);

            if (exitCode == ExitCode.SUCCESS)
            {
                // Create API-version specific output directory if testing multiple versions
                var outputDirectory = Arguments.OutputDirectory.Value;
                if (apiVersionsToTest.Count > 1 && currentApiVersion.HasValue)
                {
                    outputDirectory = Path.Combine(Arguments.OutputDirectory.Value, $"api-{currentApiVersion}");
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
                if (currentApiVersion.HasValue)
                {
                    failedApiVersions.Add(currentApiVersion.Value);
                }
                
                if (finalExitCode == ExitCode.SUCCESS)
                {
                    finalExitCode = exitCode;
                }
                
                logger.LogError($"Tests failed on API version {(currentApiVersion?.ToString() ?? "any")} with exit code: {exitCode}");
            }
            else
            {
                logger.LogInformation($"Tests passed on API version {(currentApiVersion?.ToString() ?? "any")}");
            }
        }

        // Cleanup
        runner.DeleteHeadlessFolder(Arguments.TestPath);
        runner.DeleteHeadlessFolder("runtime");

        // Stop emulators if we started them (only if user specified API versions)
        if (Arguments.ApiVersion.Value.Any())
        {
            runner.StopEmulators(Arguments.ApiVersion.ApiVersions);
        }

        // Report summary
        if (apiVersionsToTest.Count > 1)
        {
            logger.LogInformation($"=== Test Summary ===");
            logger.LogInformation($"Total API versions tested: {apiVersionsToTest.Count}");
            logger.LogInformation($"Successful: {apiVersionsToTest.Count - failedApiVersions.Count}");
            
            if (failedApiVersions.Any())
            {
                logger.LogError($"Failed API versions: {string.Join(", ", failedApiVersions)}");
            }
        }

        return finalExitCode;
    }
}
