// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.Android.Execution;
using System.Collections.Generic;
using System.IO;
using System;
using Microsoft.Extensions.Logging;
using Microsoft.DotNet.XHarness.Common.CLI;
using System.Linq;

namespace Microsoft.DotNet.XHarness.Android;

public class InstrumentationRunner
{
    public const string ReturnCodeVariableName = "return-code";

    // nunit2 one should go away eventually
    private static readonly string[] s_xmlOutputVariableNames = { "nunit2-results-path", "test-results-path" };
    private const string TestRunSummaryVariableName = "test-execution-summary";
    private const string ShortMessageVariableName = "shortMsg";
    private const string ProcessCrashedShortMessage = "Process crashed";
    private const string InstrumentationResultPrefix = "INSTRUMENTATION_RESULT:";

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
        bool logCatSucceeded;

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
                    _logger.LogError(toLog, Microsoft.DotNet.XHarness.Common.Resources.Strings.Android_ErrorPullingFiles, deviceOutputFolder);
                    failurePullingFiles = true;
                }
            }

            logCatSucceeded = _runner.TryDumpAdbLog(Path.Combine(outputDirectory, $"adb-logcat-{apkPackageName}-{(instrumentationName ?? "default")}.log"));

            if (processCrashed)
            {
                _runner.DumpBugReport(Path.Combine(outputDirectory, $"adb-bugreport-{apkPackageName}"));
            }
        }

        // In case emulator crashes halfway through, we can tell by failing to pull ADB logs from it
        if (!logCatSucceeded)
        {
            return ExitCode.SIMULATOR_FAILURE;
        }

        if (result.ExitCode == (int)AdbExitCodes.INSTRUMENTATION_TIMEOUT)
        {
            return ExitCode.TIMED_OUT;
        }

        if (processCrashed)
        {
            return ExitCode.APP_CRASH;
        }

        if (failurePullingFiles)
        {
            _logger.LogError(Microsoft.DotNet.XHarness.Common.Resources.Strings.Android_ExpectedExitCodeButFileError, instrumentationExitCode);
            return ExitCode.DEVICE_FILE_COPY_FAILURE;
        }

        if (!instrumentationExitCode.HasValue)
        {
            return ExitCode.RETURN_CODE_NOT_SET;
        }

        if (instrumentationExitCode != expectedExitCode)
        {
            _logger.LogError(Microsoft.DotNet.XHarness.Common.Resources.Strings.Android_NonSuccessInstrumentationExitCode, instrumentationExitCode, expectedExitCode);
            return ExitCode.TESTS_FAILED;
        }

        return ExitCode.SUCCESS;
    }

    private (int? ExitCode, bool Crashed, bool FilePullFailed) ParseInstrumentationResult(string apkPackageName, string outputDirectory, string result)
    {
        // This is where test instrumentation can communicate outwardly that test execution failed
        IReadOnlyDictionary<string, string> resultValues = ParseInstrumentationOutputs(result);

        // Pull XUnit result XMLs off the device
        bool failurePullingFiles = PullResultXMLs(apkPackageName, outputDirectory, resultValues)!;
        bool processCrashed = false;

        if (resultValues.TryGetValue(TestRunSummaryVariableName, out string? testRunSummary))
        {
            _logger.LogInformation(Microsoft.DotNet.XHarness.Common.Resources.Strings.Android_TestExecutionSummary, Environment.NewLine, testRunSummary);
        }

        if (resultValues.TryGetValue(ShortMessageVariableName, out string? shortMessage))
        {
            _logger.LogInformation(Microsoft.DotNet.XHarness.Common.Resources.Strings.Android_ShortMessage, Environment.NewLine, shortMessage);
            processCrashed = shortMessage.Contains(ProcessCrashedShortMessage);
        }

        // Due to the particulars of how instrumentations work, ADB will report a 0 exit code for crashed instrumentations
        // We'll change that to a specific value and print a message explaining why.
        int? instrumentationExitCode = null;
        if (resultValues.TryGetValue(ReturnCodeVariableName, out string? returnCode))
        {
            if (int.TryParse(returnCode, out int parsedExitCode))
            {
                _logger.LogInformation(Microsoft.DotNet.XHarness.Common.Resources.Strings.Android_InstrumentationFinishedNormally, parsedExitCode);
                instrumentationExitCode = parsedExitCode;
            }
            else
            {
                _logger.LogError(Microsoft.DotNet.XHarness.Common.Resources.Strings.Android_UnparseableReturnCodeValue, ReturnCodeVariableName, returnCode);
            }
        }
        else
        {
            _logger.LogError(Microsoft.DotNet.XHarness.Common.Resources.Strings.Android_NoReturnCodeProvided, ReturnCodeVariableName);
        }

        return (ExitCode: instrumentationExitCode, Crashed: processCrashed, FilePullFailed: failurePullingFiles);
    }

    private bool PullResultXMLs(string apkPackageName, string outputDirectory, IReadOnlyDictionary<string, string> resultValues)
    {
        bool success = false;

        foreach (string possibleResultKey in s_xmlOutputVariableNames)
        {
            if (!resultValues.TryGetValue(possibleResultKey, out string? resultFile))
            {
                continue;
            }

            _logger.LogInformation(Microsoft.DotNet.XHarness.Common.Resources.Strings.Android_FoundXmlResultFile, resultFile, possibleResultKey);

            try
            {
                _runner.PullFiles(apkPackageName, resultFile, outputDirectory);
            }
            catch (Exception toLog)
            {
                _logger.LogError(toLog, Microsoft.DotNet.XHarness.Common.Resources.Strings.Android_ErrorPullingFiles, resultFile);
                success = true;
            }
        }

        return success;
    }

    private IReadOnlyDictionary<string, string> ParseInstrumentationOutputs(string stdout)
    {
        var outputs = new Dictionary<string, string>();
        string[] lines = stdout.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (string line in lines.Where(line => line.StartsWith(InstrumentationResultPrefix)))
        {
            var subString = line.Substring(InstrumentationResultPrefix.Length);
            string[] results = subString.Trim().Split('=');
            if (results.Length == 2)
            {
                var key = results[0];
                var value = results[1];

                if (outputs.ContainsKey(key))
                {
                    _logger.LogWarning(Microsoft.DotNet.XHarness.Common.Resources.Strings.Android_DuplicateKey, key);
                    outputs[key] = value;
                }
                else
                {
                    outputs.Add(key, value);
                }
            }
            else
            {
                _logger.LogWarning(Microsoft.DotNet.XHarness.Common.Resources.Strings.Android_SkippingOutputLineParseFailure, line);
            }
        }

        return outputs;
    }
}
