// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Android;
using Microsoft.DotNet.XHarness.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.Extensions.Logging;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Android
{
    internal class AndroidTestCommand : TestCommand
    {
        private readonly AndroidTestCommandArguments _arguments = new AndroidTestCommandArguments();
        protected override ITestCommandArguments TestArguments => _arguments;

        // nunit2 one should go away eventually
        private readonly string[] _xmlOutputVariableNames = { "nunit2-results-path", "test-results-path" };

        public AndroidTestCommand() : base()
        {
            Options = new OptionSet() {
                "usage: android test [OPTIONS]",
                "",
                "Executes tests on and Android device, waits up to a given timeout, then copies files off the device.",
                { "arg=", "Argument to pass to the instrumentation, in form key=value", v =>
                    {
                        string[] argPair = v.Split('=');

                        if (argPair.Length != 2)
                        {
                            Options.WriteOptionDescriptions(Console.Out);
                            return;
                        }
                        else
                        {
                            _arguments.InstrumentationArguments.Add(argPair[0].Trim(), argPair[1].Trim());
                        }
                    }
                },
                { "device-out-folder=|dev-out=", "If specified, copy this folder recursively off the device to the path specified by the output directory",  v => _arguments.DeviceOutputFolder = v},
                { "instrumentation:|i:", "If specified, attempt to run instrumentation with this name instead of the default for the supplied APK.",  v => _arguments.InstrumentationName = v},
                { "package-name=|p=", "Package name contained within the supplied APK",  v => _arguments.PackageName = v},
            };

            foreach (var option in CommonOptions)
            {
                Options.Add(option);
            }
        }

        protected override Task<ExitCode> InvokeInternal()
        {
            _log.LogDebug($"Android Test command called: App = {_arguments.AppPackagePath}{Environment.NewLine}Instrumentation Name = {_arguments.InstrumentationName}");
            _log.LogDebug($"Output Directory:{_arguments.OutputDirectory}{Environment.NewLine}Timeout = {_arguments.Timeout.TotalSeconds} seconds.");
            _log.LogDebug("Arguments to instrumentation:");

            if (!File.Exists(_arguments.AppPackagePath))
            {
                _log.LogCritical($"Couldn't find {_arguments.AppPackagePath}!");
                return Task.FromResult(ExitCode.PACKAGE_NOT_FOUND);
            }
            var runner = new AdbRunner(_log);

            // Package Name is not guaranteed to match file name, so it needs to be mandatory.
            string apkPackageName = _arguments.PackageName;

            int instrumentationExitCode = (int)ExitCode.GENERAL_FAILURE;

            try
            {
                using (_log.BeginScope("Initialization and setup of APK on device"))
                {
                    runner.KillAdbServer();
                    runner.StartAdbServer();
                    runner.ClearAdbLog();

                    _log.LogDebug($"Working with {runner.GetAdbVersion()}");

                    // If anything changed about the app, Install will fail; uninstall it first.
                    // (we'll ignore if it's not present)
                    runner.UninstallApk(apkPackageName);
                    if (runner.InstallApk(_arguments.AppPackagePath) != 0)
                    {
                        _log.LogCritical("Install failure: Test command cannot continue");
                        return Task.FromResult(ExitCode.PACKAGE_INSTALLATION_FAILURE);
                    }
                    runner.KillApk(apkPackageName);
                }

                // No class name = default Instrumentation
                (string stdOut, _, int exitCode) = runner.RunApkInstrumentation(apkPackageName, _arguments.InstrumentationName, _arguments.InstrumentationArguments, _arguments.Timeout);

                using (_log.BeginScope("Post-test copy and cleanup"))
                {
                    if (exitCode == (int)ExitCode.SUCCESS)
                    {
                        (var resultValues, var instrExitCode) = ParseInstrumentationOutputs(stdOut);

                        instrumentationExitCode = instrExitCode;

                        foreach (string possibleResultKey in _xmlOutputVariableNames)
                        {
                            if (resultValues.ContainsKey(possibleResultKey))
                            {
                                _log.LogInformation($"Found XML result file: '{resultValues[possibleResultKey]}'(key: {possibleResultKey})");
                                runner.PullFiles(resultValues[possibleResultKey], _arguments.OutputDirectory);
                            }
                        }
                    }

                    // Optionally copy off an entire folder
                    if (!string.IsNullOrEmpty(_arguments.DeviceOutputFolder))
                    {
                        var logs = runner.PullFiles(_arguments.DeviceOutputFolder, _arguments.OutputDirectory);
                        foreach (string log in logs)
                        {
                            _log.LogDebug($"Detected output file: {log}");
                        }
                    }
                    runner.DumpAdbLog(Path.Combine(_arguments.OutputDirectory, $"adb-logcat-{_arguments.PackageName}.log"));
                    runner.UninstallApk(apkPackageName);
                }

                if (instrumentationExitCode != (int)ExitCode.SUCCESS)
                {
                    _log.LogError($"Non-success instrumentation exit code: {instrumentationExitCode}");
                }
                else
                {
                    return Task.FromResult(ExitCode.SUCCESS);
                }
            }
            catch (Exception toLog)
            {
                _log.LogCritical(toLog, $"Failure to run test package: {toLog.Message}");
            }
            finally
            {
                runner.KillAdbServer();
            }

            return Task.FromResult(ExitCode.GENERAL_FAILURE);
        }

        private (Dictionary<string, string> values, int exitCode) ParseInstrumentationOutputs(string stdOut)
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
                            _log.LogWarning($"Key '{results[0]}' defined more than once");
                            outputs[results[0]] = results[1];
                        }
                        else
                        {
                            outputs.Add(results[0], results[1]);
                        }
                    }
                    else
                    {
                        _log.LogWarning($"Skipping output line due to key-value-pair parse failure: '{line}'");
                    }
                }
                else if (line.StartsWith(exitCodePrefix))
                {
                    if (!int.TryParse(line.Substring(exitCodePrefix.Length).Trim(), out exitCode))
                    {
                        _log.LogError($"Failure parsing ADB Exit code from line: '{line}'");
                    }
                }
            }
            return (outputs, exitCode);
        }
    }
}
