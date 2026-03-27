// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.Android.Execution;
using System.Collections.Generic;
using System.IO;
using System;
using Microsoft.Extensions.Logging;
using Microsoft.DotNet.XHarness.Common;
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
        var producedFiles = new List<DiagnosticsFile>();

        // No class name = default Instrumentation
        ProcessExecutionResults result = _runner.RunApkInstrumentation(apkPackageName, instrumentationName, instrumentationArguments, timeout);

        bool processCrashed = false;
        bool failurePullingFiles = false;
        bool logCatSucceeded;

        using (_logger.BeginScope("Post-test copy and cleanup"))
        {
            if (result.ExitCode == (int)ExitCode.SUCCESS)
            {
                (instrumentationExitCode, processCrashed, failurePullingFiles) = ParseInstrumentationResult(apkPackageName, outputDirectory, result.StandardOutput, producedFiles);
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
                        producedFiles.Add(new DiagnosticsFile
                        {
                            Name = Path.GetFileName(log),
                            Type = "device-output",
                            Path = log,
                        });
                    }
                }
                catch (Exception toLog)
                {
                    _logger.LogError(toLog, "Hit error (typically permissions) trying to pull {filePathOnDevice}", deviceOutputFolder);
                    failurePullingFiles = true;
                }
            }

            var logcatFileName = $"adb-logcat-{apkPackageName}-{(instrumentationName ?? "default")}.log";
            var logcatFilePath = Path.Combine(outputDirectory, logcatFileName);
            logCatSucceeded = _runner.TryDumpAdbLog(logcatFilePath);

            if (logCatSucceeded)
            {
                producedFiles.Add(new DiagnosticsFile
                {
                    Name = logcatFileName,
                    Type = "logcat",
                    Path = logcatFilePath,
                });
            }

            if (processCrashed)
            {
                var bugreportPath = _runner.DumpBugReport(Path.Combine(outputDirectory, $"adb-bugreport-{apkPackageName}"));
                if (!string.IsNullOrEmpty(bugreportPath))
                {
                    producedFiles.Add(new DiagnosticsFile
                    {
                        Name = Path.GetFileName(bugreportPath),
                        Type = "bugreport",
                        Path = bugreportPath,
                    });
                }
            }
        }

        // Determine exit code
        ExitCode exitCode;

        // In case emulator crashes halfway through, we can tell by failing to pull ADB logs from it
        if (!logCatSucceeded)
        {
            exitCode = ExitCode.SIMULATOR_FAILURE;
        }
        else if (result.ExitCode == (int)AdbExitCodes.INSTRUMENTATION_TIMEOUT)
        {
            exitCode = ExitCode.TIMED_OUT;
        }
        else if (processCrashed)
        {
            exitCode = ExitCode.APP_CRASH;
        }
        else if (failurePullingFiles)
        {
            _logger.LogError($"Received expected instrumentation exit code ({instrumentationExitCode}), " +
                             "but we hit errors pulling files from the device (see log for details.)");
            exitCode = ExitCode.DEVICE_FILE_COPY_FAILURE;
        }
        else if (!instrumentationExitCode.HasValue)
        {
            exitCode = ExitCode.RETURN_CODE_NOT_SET;
        }
        else if (instrumentationExitCode != expectedExitCode)
        {
            _logger.LogError($"Non-success instrumentation exit code: {instrumentationExitCode}, expected: {expectedExitCode}");
            exitCode = ExitCode.TESTS_FAILED;
        }
        else
        {
            exitCode = ExitCode.SUCCESS;
        }

        EmitRunSummary(exitCode, instrumentationExitCode, producedFiles);

        return exitCode;
    }

    private (int? ExitCode, bool Crashed, bool FilePullFailed) ParseInstrumentationResult(string apkPackageName, string outputDirectory, string result, List<DiagnosticsFile> producedFiles)
    {
        // This is where test instrumentation can communicate outwardly that test execution failed
        IReadOnlyDictionary<string, string> resultValues = ParseInstrumentationOutputs(result);

        // Pull XUnit result XMLs off the device
        bool failurePullingFiles = PullResultXMLs(apkPackageName, outputDirectory, resultValues, producedFiles)!;
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
        int? instrumentationExitCode = null;
        if (resultValues.TryGetValue(ReturnCodeVariableName, out string? returnCode))
        {
            if (int.TryParse(returnCode, out int parsedExitCode))
            {
                _logger.LogInformation($"Instrumentation finished normally with exit code {parsedExitCode}");
                instrumentationExitCode = parsedExitCode;
            }
            else
            {
                _logger.LogError($"Un-parse-able value for '{ReturnCodeVariableName}' : '{returnCode}'");
            }
        }
        else
        {
            _logger.LogError($"No value for '{ReturnCodeVariableName}' provided in instrumentation result. This may indicate a crashed test (see log)");
        }

        return (ExitCode: instrumentationExitCode, Crashed: processCrashed, FilePullFailed: failurePullingFiles);
    }

    private bool PullResultXMLs(string apkPackageName, string outputDirectory, IReadOnlyDictionary<string, string> resultValues, List<DiagnosticsFile> producedFiles)
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
                producedFiles.Add(new DiagnosticsFile
                {
                    Name = Path.GetFileName(resultFile),
                    Type = "test-results",
                    Path = Path.Combine(outputDirectory, Path.GetFileName(resultFile)),
                });
            }
            catch (Exception toLog)
            {
                _logger.LogError(toLog, "Hit error (typically permissions) trying to pull {filePathOnDevice}", resultFile);
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
                    _logger.LogWarning($"Key '{key}' defined more than once");
                    outputs[key] = value;
                }
                else
                {
                    outputs.Add(key, value);
                }
            }
            else
            {
                _logger.LogWarning($"Skipping output line due to key-value-pair parse failure: '{line}'");
            }
        }

        return outputs;
    }

    private void EmitRunSummary(ExitCode exitCode, int? instrumentationExitCode, List<DiagnosticsFile> producedFiles)
    {
        var device = _runner.GetActiveDevice();
        string? deviceOsVersion = device?.ApiVersion.HasValue == true ? $"API {device.ApiVersion}" : null;

        RunSummaryEmitter.EmitRunSummary(
            _logger,
            exitCode,
            platform: "android",
            deviceName: device?.DeviceSerial,
            deviceOsVersion: deviceOsVersion,
            architecture: device?.Architecture,
            instrumentationExitCode: instrumentationExitCode,
            producedFiles: producedFiles);
    }
}
