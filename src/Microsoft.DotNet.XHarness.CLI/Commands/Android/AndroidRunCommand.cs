using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Android;
using Microsoft.DotNet.XHarness.Android.Execution;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.Common.CLI.Commands;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Android
{
    internal class AndroidRunCommand : XHarnessCommand
    {
        // nunit2 one should go away eventually
        private static readonly string[] s_xmlOutputVariableNames = { "nunit2-results-path", "test-results-path" };
        private const string TestRunSummaryVariableName = "test-execution-summary";
        private const string ShortMessageVariableName = "shortMsg";
        private const string ReturnCodeVariableName = "return-code";
        private const string ProcessCrashedShortMessage = "Process crashed";

        private readonly AndroidRunCommandArguments _arguments = new();

        protected override XHarnessCommandArguments Arguments => _arguments;

        protected override string CommandUsage { get; } = "android run --output-directory=... --package-name=... [OPTIONS]";

        private const string CommandHelp = "Run tests using an already installed .apk on an Android device";
        protected override string CommandDescription { get; } = @$"
{CommandHelp}

APKs can communicate status back to XHarness using the parameters:

Required:
{ReturnCodeVariableName} - Exit code for instrumentation. Necessary because a crashing instrumentation may be indistinguishable from a passing one based solely on the exit code.
 
Arguments:
";

        public AndroidRunCommand() : base("run", false, CommandHelp)
        {
        }

        protected override Task<ExitCode> InvokeInternal(ILogger logger)
        {
            logger.LogDebug($"Android Run command called: App = {_arguments.PackageName}{Environment.NewLine}");
            logger.LogDebug($"Timeout = {_arguments.Timeout.TotalSeconds} seconds.");

            // Package Name is not guaranteed to match file name, so it needs to be mandatory.
            string apkPackageName = _arguments.PackageName;

            var runner = new AdbRunner(logger);

            // Make sure the adb server is started
            runner.StartAdbServer();

            var deviceId = _arguments.DeviceId;

            if (string.IsNullOrEmpty(deviceId))
            {
                // trying to find out if there is only one device with the app installed
                deviceId = runner.GetUniqueDeviceToUse(logger, "package:" + apkPackageName, "app");
                if (string.IsNullOrEmpty(deviceId))
                {
                    return Task.FromResult(ExitCode.ADB_DEVICE_ENUMERATION_FAILURE);
                }
            }

            runner.SetActiveDevice(deviceId);

            // Wait til at least device(s) are ready
            runner.WaitForDevice();

            // Empty log as we'll be uploading the full logcat for this execution
            runner.ClearAdbLog();

            logger.LogDebug($"Working with {runner.GetAdbVersion()}");

            return Task.FromResult(InvokeHelper(
                logger,
                apkPackageName,
                _arguments.InstrumentationName,
                _arguments.InstrumentationArguments,
                _arguments.OutputDirectory,
                _arguments.DeviceOutputFolder,
                _arguments.Timeout,
                _arguments.ExpectedExitCode,
                runner));
        }

        public static ExitCode InvokeHelper(
            ILogger logger,
            string apkPackageName,
            string? instrumentationName,
            Dictionary<string, string> instrumentationArguments,
            string outputDirectory,
            string? deviceOutputFolder,
            TimeSpan timeout,
            int expectedExitCode,
            AdbRunner runner)
        {

            int instrumentationExitCode = (int)ExitCode.GENERAL_FAILURE;

            try
            {
                // No class name = default Instrumentation
                ProcessExecutionResults? result = runner.RunApkInstrumentation(apkPackageName, instrumentationName, instrumentationArguments, timeout);
                bool processCrashed = false;
                bool failurePullingFiles = false;

                using (logger.BeginScope("Post-test copy and cleanup"))
                {
                    if (result.ExitCode == (int)ExitCode.SUCCESS)
                    {
                        Dictionary<string, string> resultValues;
                        // This is where test instrumentation can communicate outwardly that test execution failed
                        (resultValues, instrumentationExitCode) = ParseInstrumentationOutputs(logger, result.StandardOutput);

                        // Pull XUnit result XMLs off the device
                        foreach (string possibleResultKey in s_xmlOutputVariableNames)
                        {
                            if (resultValues.ContainsKey(possibleResultKey))
                            {
                                logger.LogInformation($"Found XML result file: '{resultValues[possibleResultKey]}'(key: {possibleResultKey})");
                                try
                                {
                                    runner.PullFiles(resultValues[possibleResultKey], outputDirectory);
                                }
                                catch (Exception toLog)
                                {
                                    logger.LogError(toLog, "Hit error (typically permissions) trying to pull {filePathOnDevice}", resultValues[possibleResultKey]);
                                    failurePullingFiles = true;
                                }
                            }
                        }
                        if (resultValues.ContainsKey(TestRunSummaryVariableName))
                        {
                            logger.LogInformation($"Test execution summary:{Environment.NewLine}{resultValues[TestRunSummaryVariableName]}");
                        }
                        if (resultValues.ContainsKey(ShortMessageVariableName))
                        {
                            logger.LogInformation($"Short Message: {Environment.NewLine}{resultValues[ShortMessageVariableName]}");
                            processCrashed = resultValues[ShortMessageVariableName].Contains(ProcessCrashedShortMessage);
                        }

                        // Due to the particulars of how instrumentations work, ADB will report a 0 exit code for crashed instrumentations
                        // We'll change that to a specific value and print a message explaining why.
                        if (resultValues.ContainsKey(ReturnCodeVariableName))
                        {
                            if (int.TryParse(resultValues[ReturnCodeVariableName], out int bundleExitCode))
                            {
                                logger.LogInformation($"Instrumentation finished normally with exit code {bundleExitCode}");
                                instrumentationExitCode = bundleExitCode;
                            }
                            else
                            {
                                logger.LogError($"Un-parse-able value for '{ReturnCodeVariableName}' : '{resultValues[ReturnCodeVariableName]}'");
                                instrumentationExitCode = (int)ExitCode.RETURN_CODE_NOT_SET;
                            }
                        }
                        else
                        {
                            logger.LogError($"No value for '{ReturnCodeVariableName}' provided in instrumentation result.  This may indicate a crashed test (see log)");
                            instrumentationExitCode = (int)ExitCode.RETURN_CODE_NOT_SET;
                        }
                    }

                    // Optionally copy off an entire folder
                    if (!string.IsNullOrEmpty(deviceOutputFolder))
                    {
                        try
                        {
                            var logs = runner.PullFiles(deviceOutputFolder, outputDirectory);
                            foreach (string log in logs)
                            {
                                logger.LogDebug($"Found output file: {log}");
                            }
                        }
                        catch (Exception toLog)
                        {
                            logger.LogError(toLog, "Hit error (typically permissions) trying to pull {filePathOnDevice}", deviceOutputFolder);
                            failurePullingFiles = true;
                        }
                    }

                    runner.DumpAdbLog(Path.Combine(outputDirectory, $"adb-logcat-{apkPackageName}.log"));

                    if (processCrashed)
                    {
                        runner.DumpBugReport(Path.Combine(outputDirectory, $"adb-bugreport-{apkPackageName}.zip"));
                    }
                }

                if (instrumentationExitCode != expectedExitCode)
                {
                    logger.LogError($"Non-success instrumentation exit code: {instrumentationExitCode}, expected: {expectedExitCode}");
                }
                else if (failurePullingFiles)
                {
                    logger.LogError($"Received expected instrumentation exit code ({instrumentationExitCode}), but we hit errors pulling files from the device (see log for details.)");
                    return ExitCode.DEVICE_FILE_COPY_FAILURE;
                }
                else
                {
                    return ExitCode.SUCCESS;
                }
            }
            catch (Exception toLog)
            {
                logger.LogCritical(toLog, $"Failure to run test package: {toLog.Message}");
            }

            return ExitCode.GENERAL_FAILURE;
        }

        private static (Dictionary<string, string> values, int exitCode) ParseInstrumentationOutputs(ILogger logger, string stdOut)
        {
            // If ADB.exe's output changes (which we control when we take updates in this repo), we'll need to fix this.
            string resultPrefix = "INSTRUMENTATION_RESULT:";
            string exitCodePrefix = "INSTRUMENTATION_CODE:";
            int exitCode = -1;
            var outputs = new Dictionary<string, string>();
            string[] lines = stdOut.Split(Environment.NewLine);

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
                            logger.LogWarning($"Key '{results[0]}' defined more than once");
                            outputs[results[0]] = results[1];
                        }
                        else
                        {
                            outputs.Add(results[0], results[1]);
                        }
                    }
                    else
                    {
                        logger.LogWarning($"Skipping output line due to key-value-pair parse failure: '{line}'");
                    }
                }
                else if (line.StartsWith(exitCodePrefix))
                {
                    if (!int.TryParse(line.Substring(exitCodePrefix.Length).Trim(), out exitCode))
                    {
                        logger.LogError($"Failure parsing ADB Exit code from line: '{line}'");
                    }
                }
            }

            return (outputs, exitCode);
        }
    }
}
