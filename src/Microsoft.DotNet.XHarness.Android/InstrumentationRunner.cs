﻿using Microsoft.DotNet.XHarness.Android.Execution;
using System.Collections.Generic;
using System.IO;
using System;
using Microsoft.Extensions.Logging;
using Microsoft.DotNet.XHarness.Common.CLI;

namespace Microsoft.DotNet.XHarness.Android;

public class InstrumentationRunner
{
    public const string ReturnCodeVariableName = "return-code";

    // nunit2 one should go away eventually
    private static readonly string[] s_xmlOutputVariableNames = { "nunit2-results-path", "test-results-path" };
    private const string TestRunSummaryVariableName = "test-execution-summary";
    private const string ShortMessageVariableName = "shortMsg";
    private const string ProcessCrashedShortMessage = "Process crashed";

    private readonly ILogger _logger;
    private readonly AdbRunner _runner;
    
    public InstrumentationRunner(ILogger logger, AdbRunner runner)
    {
        _logger = logger;
        _runner = runner;
    }
    
    public ExitCode RunApkInstrumentation(
        string apkPackageName,
        string? instrumentationName,
        Dictionary<string, string> instrumentationArguments,
        string outputDirectory,
        string? deviceOutputFolder,
        TimeSpan timeout,
        int expectedExitCode)
    {
        int? instrumentationExitCode = null;

        // No class name = default Instrumentation
        ProcessExecutionResults result = _runner.RunApkInstrumentation(apkPackageName, instrumentationName, instrumentationArguments, timeout);

        bool processCrashed = false;
        bool failurePullingFiles = false;

        using (_logger.BeginScope("Post-test copy and cleanup"))
        {
            if (result.ExitCode == (int)ExitCode.SUCCESS)
            {
                (instrumentationExitCode, processCrashed, failurePullingFiles) = ParseInstrumentationResult(apkPackageName, outputDirectory, result.StandardOutput);
            }

            // Optionally copy off an entire folder
            if (!string.IsNullOrEmpty(deviceOutputFolder))
            {
                try
                {
                    var logs = _runner.PullFiles(apkPackageName, deviceOutputFolder, outputDirectory);
                    foreach (string log in logs)
                    {
                        _logger.LogDebug($"Found output file: {log}");
                    }
                }
                catch (Exception toLog)
                {
                    _logger.LogError(toLog, "Hit error (typically permissions) trying to pull {filePathOnDevice}", deviceOutputFolder);
                    failurePullingFiles = true;
                }
            }

            _runner.DumpAdbLog(Path.Combine(outputDirectory, $"adb-logcat-{apkPackageName}-{(instrumentationName ?? "default")}.log"));

            if (processCrashed)
            {
                _runner.DumpBugReport(Path.Combine(outputDirectory, $"adb-bugreport-{apkPackageName}"));
            }
        }

        if (processCrashed)
        {
            return ExitCode.APP_CRASH;
        }

        if (failurePullingFiles)
        {
            _logger.LogError($"Received expected instrumentation exit code ({instrumentationExitCode}), " +
                             "but we hit errors pulling files from the device (see log for details.)");
            return ExitCode.DEVICE_FILE_COPY_FAILURE;
        }

        if (!instrumentationExitCode.HasValue)
        {
            return ExitCode.RETURN_CODE_NOT_SET;
        }

        if (instrumentationExitCode != expectedExitCode)
        {
            _logger.LogError($"Non-success instrumentation exit code: {instrumentationExitCode}, expected: {expectedExitCode}");
            return ExitCode.TESTS_FAILED;
        }

        return ExitCode.SUCCESS;
    }

    private (int?, bool, bool) ParseInstrumentationResult(string apkPackageName, string outputDirectory, string result)
    {
        int? instrumentationExitCode;
        Dictionary<string, string> resultValues;
        // This is where test instrumentation can communicate outwardly that test execution failed
        (resultValues, instrumentationExitCode) = ParseInstrumentationOutputs(result);

        // Pull XUnit result XMLs off the device
        bool failurePullingFiles = PullResultXMLs(apkPackageName, outputDirectory, resultValues)!;
        bool processCrashed = false;

        if (resultValues.TryGetValue(TestRunSummaryVariableName, out string? testRunSummary))
        {
            _logger.LogInformation($"Test execution summary:{Environment.NewLine}{testRunSummary}");
        }

        if (resultValues.TryGetValue(ShortMessageVariableName, out string? shortMessage))
        {
            _logger.LogInformation($"Short message:{Environment.NewLine}{shortMessage}");
            processCrashed = shortMessage.Contains(ProcessCrashedShortMessage);
        }

        // Due to the particulars of how instrumentations work, ADB will report a 0 exit code for crashed instrumentations
        // We'll change that to a specific value and print a message explaining why.
        if (resultValues.TryGetValue(ReturnCodeVariableName, out string? returnCode))
        {
            if (int.TryParse(returnCode, out int bundleExitCode))
            {
                _logger.LogInformation($"Instrumentation finished normally with exit code {bundleExitCode}");
                instrumentationExitCode = bundleExitCode;
            }
            else
            {
                _logger.LogError($"Un-parse-able value for '{ReturnCodeVariableName}' : '{returnCode}'");
                instrumentationExitCode = null;
            }
        }
        else
        {
            _logger.LogError($"No value for '{ReturnCodeVariableName}' provided in instrumentation result. This may indicate a crashed test (see log)");
            instrumentationExitCode ??= null;
        }

        return (instrumentationExitCode, processCrashed, failurePullingFiles);
    }

    private bool PullResultXMLs(string apkPackageName, string outputDirectory, Dictionary<string, string> resultValues)
    {
        bool success = false;

        foreach (string possibleResultKey in s_xmlOutputVariableNames)
        {
            if (!resultValues.TryGetValue(possibleResultKey, out string? resultFile))
            {
                continue;
            }

            _logger.LogInformation($"Found XML result file: '{resultFile}'(key: {possibleResultKey})");

            try
            {
                _runner.PullFiles(apkPackageName, resultFile, outputDirectory);
            }
            catch (Exception toLog)
            {
                _logger.LogError(toLog, "Hit error (typically permissions) trying to pull {filePathOnDevice}", resultFile);
                success = true;
            }
        }

        return success;
    }

    private (Dictionary<string, string> values, int exitCode) ParseInstrumentationOutputs(string stdout)
    {
        // If ADB.exe's output changes (which we control when we take updates in this repo), we'll need to fix this.
        string resultPrefix = "INSTRUMENTATION_RESULT:";
        string exitCodePrefix = "INSTRUMENTATION_CODE:";
        int exitCode = -1;
        var outputs = new Dictionary<string, string>();
        string[] lines = stdout.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (string line in lines)
        {
            if (line.StartsWith(resultPrefix))
            {
                var subString = line.Substring(resultPrefix.Length);
                string[] results = subString.Trim().Split('=');
                if (results.Length == 2)
                {
                    if (outputs.ContainsKey(results[0]))
                    {
                        _logger.LogWarning($"Key '{results[0]}' defined more than once");
                        outputs[results[0]] = results[1];
                    }
                    else
                    {
                        outputs.Add(results[0], results[1]);
                    }
                }
                else
                {
                    _logger.LogWarning($"Skipping output line due to key-value-pair parse failure: '{line}'");
                }
            }
            else if (line.StartsWith(exitCodePrefix))
            {
                if (!int.TryParse(line.Substring(exitCodePrefix.Length).Trim(), out var ec))
                {
                    _logger.LogError($"Failure parsing ADB Exit code from line: '{line}'");
                }
                else
                {
                    exitCode = ec;
                }
            }
        }

        return (outputs, exitCode);
    }
}
