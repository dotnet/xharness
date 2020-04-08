// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.Android
{
    public class AdbRunner
    {
        #region Constructor and state variables

        private const string AdbEnvironmentVariableName = "ADB_EXE_PATH";

        private readonly string _absoluteAdbExePath;
        private readonly ILogger _log;

        public AdbRunner(ILogger log, string adbExePath = ".\\adb.exe")
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));

            // We need to find ADB.exe somewhere
            string environmentPath = Environment.GetEnvironmentVariable(AdbEnvironmentVariableName);
            if (!string.IsNullOrEmpty(environmentPath))
            {
                _log.LogDebug($"Using {AdbEnvironmentVariableName} environment variable ({environmentPath}) for ADB path.");
                adbExePath = environmentPath;
            }
            _absoluteAdbExePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(adbExePath));
            if (!File.Exists(_absoluteAdbExePath))
            {
                _log.LogError($"Unable to find adb.exe");
                throw new FileNotFoundException($"Could not find adb.exe. Either set it in the environment via {AdbEnvironmentVariableName} or call with valid path (provided:  '{adbExePath}')");
            }
            if (!_absoluteAdbExePath.Equals(adbExePath))
            {
                _log.LogDebug($"ADBRunner using ADB.exe supplied from {adbExePath}");
                _log.LogDebug($"Full resolved path:'{_absoluteAdbExePath}'");
            }
        }

        #endregion

        #region Functions

        public string GetAdbVersion() => RunAdbCommand("version").StandardOutput;

        public string GetAdbState() => RunAdbCommand("get-state").StandardOutput;

        public void ClearAdbLog() => RunAdbCommand("logcat -c");

        public void DumpAdbLog(string outputFilePath, string filterSpec = "")
        {
            // This is somehow reaching full timeout, have to figure it out, allow 20s for demo?
            var result = RunAdbCommand($"logcat -d {filterSpec}");
            if (result.ExitCode != 0)
            {
                // Could throw here, but it would tear down a possibly otherwise acceptable execution.
                _log.LogError($"Error getting ADB log:{Environment.NewLine}{FormatProcessOutputs(result)}");
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath));
                File.WriteAllText(outputFilePath, result.StandardOutput);
                _log.LogInformation($"Wrote current ADB log to {outputFilePath}");
            }
        }

        public void StartAdbServer()
        {
            var result = RunAdbCommand("start-server");
            _log.LogDebug($"{result.StandardOutput}");
            if (result.ExitCode != 0)
            {
                throw new Exception($"Error starting ADB Server.  Std out:{result.StandardOutput} Std. Err: {result.StandardError}");
            }
        }

        public void KillAdbServer()
        {
            var result = RunAdbCommand("kill-server");
            if (result.ExitCode != 0)
            {
                throw new Exception($"Error killing ADB Server.  Std out:{result.StandardOutput} Std. Err: {result.StandardError}");
            }
        }

        public int InstallApk(string apkPath)
        {
            _log.LogInformation($"Attempting to install {apkPath}: ");
            if (string.IsNullOrEmpty(apkPath))
            {
                throw new ArgumentNullException($"No value supplied for {nameof(apkPath)} ");
            }
            if (!File.Exists(apkPath))
            {
                throw new FileNotFoundException($"Could not find {apkPath}");
            }
            var result = RunAdbCommand($"install \"{apkPath}\"");
            if (result.ExitCode != 0)
            {
                _log.LogError($"Error:{Environment.NewLine}{FormatProcessOutputs(result)}");
            }
            else
            {
                _log.LogInformation($"Successfully installed {apkPath}.");
            }
            return result.ExitCode;
        }

        public int UninstallApk(string apkName)
        {
            if (string.IsNullOrEmpty(apkName))
            {
                throw new ArgumentNullException(nameof(apkName));
            }

            _log.LogInformation($"Attempting to remove apk '{apkName}': ");
            var result = RunAdbCommand($"uninstall {apkName}");
            if (result.ExitCode == (int)AdbExitCodes.SUCCESS)
            {
                _log.LogInformation($"Successfully uninstalled {apkName}.");
            }
            else if (result.ExitCode == (int)AdbExitCodes.ADB_UNINSTALL_APP_NOT_ON_DEVICE)
            {
                _log.LogInformation($"APK '{apkName}' not on device.");
            }
            else
            {
                _log.LogError($"Error: {FormatProcessOutputs(result)}");
            }
            return result.ExitCode;
        }

        // This function works but given we'll likely only be using Instrumentations doesn't matter.
        public int KillApk(string apkName)
        {
            _log.LogInformation($"Killing all running processes for '{apkName}': ");
            var result = RunAdbCommand($"shell am kill --user all {apkName}");
            if (result.ExitCode != (int)AdbExitCodes.SUCCESS)
            {
                _log.LogError($"Error:{Environment.NewLine}{FormatProcessOutputs(result)}");
            }
            else
            {
                _log.LogDebug($"Success!{Environment.NewLine}{result.StandardOutput}");
            }
            return result.ExitCode;
        }

        public void GrantPermissions(string apkName, string[] permissions)
        {
            _log.LogInformation($"Granting permissions to '{apkName}' ");

            foreach (string permission in permissions)
            {
                var result = RunAdbCommand($"shell pm grant {apkName} {permission}");

                if (result.ExitCode != (int)AdbExitCodes.SUCCESS)
                {
                    _log.LogError($"Failed granting '{permission}': Exit code: {result.ExitCode}{Environment.NewLine}{result.StandardOutput}; execution may fail as a result.");
                }
                else
                {
                    _log.LogDebug($"Successfully granted permission:{Environment.NewLine}{permission}");
                }
            }
        }

        // Assumes the directory is empty so any files present after the pull are new.
        public List<string> PullFiles(string devicePath, string localPath)
        {
            if (string.IsNullOrEmpty(localPath))
            {
                throw new ArgumentNullException(nameof(localPath));
            }

            string tempFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                Directory.CreateDirectory(tempFolder);
                Directory.CreateDirectory(localPath);
                _log.LogInformation($"Attempting to pull {devicePath} to {localPath}");
                List<string> copiedFiles = new List<string>();

                var result = RunAdbCommand($"pull {devicePath} {tempFolder}");

                if (result.ExitCode != (int)AdbExitCodes.SUCCESS)
                {
                    _log.LogError($"ERROR: {FormatProcessOutputs(result)}");
                }
                else
                {
                    var copiedToTemp = Directory.GetFiles(tempFolder, "*", SearchOption.AllDirectories);
                    foreach (var filePath in copiedToTemp)
                    {

                        var relativePath = Path.GetRelativePath(tempFolder, filePath);
                        var destinationPath = Path.Combine(localPath, relativePath);
                        // if the file is already there, just warn and skip it.
                        if (File.Exists(destinationPath))
                        {
                            _log.LogWarning($"Skipping file copy as {destinationPath} already exists.");
                        }
                        else
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                            File.Move(filePath, destinationPath);
                            copiedFiles.Add(destinationPath);
                        }
                    }
                }
                _log.LogDebug($"Copied {copiedFiles.Count} files to {localPath}.");
                return copiedFiles;
            }
            finally
            {
                Directory.Delete(tempFolder, true);
            }
        }

        public void PushFiles(string localDirectory, string devicePath, bool removeIfPresent)
        {
            _log.LogInformation($"Pushing contents of '{localDirectory}' to '{devicePath}'");

            if (string.IsNullOrEmpty(localDirectory))
            {
                throw new ArgumentNullException(nameof(localDirectory));
            }
            if (!Directory.Exists(localDirectory))
            {
                throw new DirectoryNotFoundException($"{localDirectory} does not exist");
            }

            // For now, we'll hard-code the assumption that files go into /sdcard/Documents.
            // This functionality may not end up getting used, so don't need to polish it until we're sure it is.
            if (removeIfPresent && devicePath.StartsWith("/sdcard/Documents/", StringComparison.OrdinalIgnoreCase))
            {
                RunAdbCommand($"shell rm -rf {devicePath}");
            }
            string[] filesToCopy = Directory.GetFiles(localDirectory, "*", SearchOption.AllDirectories);

            foreach (string filePath in filesToCopy)
            {
                string relativeFilePath = Path.GetRelativePath(localDirectory, filePath).Replace("\\", "/");
                var result = RunAdbCommand($"push \"{filePath}\" {devicePath}{relativeFilePath}");

                if (result.ExitCode != (int)AdbExitCodes.SUCCESS)
                {
                    Exception theException = new Exception($"ERROR: {FormatProcessOutputs(result)}");
                    _log.LogError(theException, "Failure pushing files");
                    throw theException;
                }
            }
            _log.LogDebug($"Copied {filesToCopy.Length} files");
        }

        public (string StandardOutput, string StandardError, int ExitCode) RunApkInstrumentation(string apkName)
        {
            return RunApkInstrumentation(apkName, "", new Dictionary<string, string>());
        }

        public (string StandardOutput, string StandardError, int ExitCode) RunApkInstrumentation(string apkName, Dictionary<string, string> args) =>
            RunApkInstrumentation(apkName, "", args);


        public (string StandardOutput, string StandardError, int ExitCode) RunApkInstrumentation(string apkName, string instrumentationClassName, Dictionary<string, string> args)
        {
            string displayName = string.IsNullOrEmpty(instrumentationClassName) ? "{default}" : instrumentationClassName;

            string appArguments = "";
            if (args.Count > 0)
            {
                foreach (string key in args.Keys)
                {
                    appArguments = $"{appArguments} -e {key} {args[key]}";
                }
            }

            string command = $"shell am instrument {appArguments} -w {apkName}";
            if (string.IsNullOrEmpty(instrumentationClassName))
            {
                _log.LogInformation($"Starting default instrumentation class on {apkName} (exit code -1 == success)");
            }
            else
            {
                _log.LogInformation($"Starting instrumentation class '{instrumentationClassName}' on {apkName}");
                command = $"{command}/{instrumentationClassName}";
            }
            _log.LogDebug($"Raw command: '{command}'");

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            var result = RunAdbCommand(command);
            stopWatch.Stop();

            _log.LogInformation($"Running instrumentation class {displayName} took {stopWatch.Elapsed.TotalSeconds} seconds");
            _log.LogDebug(FormatProcessOutputs(result));
            return result;
        }

        #endregion

        #region Process runner helpers

        private string FormatProcessOutputs((string StandardOutput, string StandardError, int ExitCode) result)
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine($"Exit code: {result.ExitCode}");
            output.AppendLine($"Standard Output:{Environment.NewLine}{result.StandardOutput}");
            if (!string.IsNullOrEmpty(result.StandardError))
            {
                output.AppendLine($"Standard Error:{Environment.NewLine}{result.StandardOutput}");
            }
            return output.ToString();
        }

        public (string StandardOutput, string StandardError, int ExitCode) RunAdbCommand(string command)
        {
            return RunAdbCommand(command, TimeSpan.FromMinutes(5));
        }

        public (string StandardOutput, string StandardError, int ExitCode) RunAdbCommand(string command, TimeSpan timeOut)
        {
            if (!File.Exists(_absoluteAdbExePath))
            {
                throw new FileNotFoundException($"Provided path for adb.exe was not valid ('{_absoluteAdbExePath}')");
            }

            _log.LogDebug($"Executing command: '{_absoluteAdbExePath} {command}'");

            ProcessStartInfo processStartInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(_absoluteAdbExePath),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                FileName = _absoluteAdbExePath,
                Arguments = command,
            };
            Process p = new Process() { StartInfo = processStartInfo };
            StringBuilder standardOut = new StringBuilder();
            StringBuilder standardErr = new StringBuilder();

            p.OutputDataReceived += delegate (object sender, DataReceivedEventArgs e)
            {
                lock (standardOut)
                {
                    if (e.Data != null)
                        standardOut.AppendLine(e.Data);
                }
            };

            p.ErrorDataReceived += delegate (object sender, DataReceivedEventArgs e)
            {
                lock (standardErr)
                {
                    if (e.Data != null)
                        standardErr.AppendLine(e.Data);
                }
            };

            p.Start();

            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            // Allow the process time to send messages to the above delegates
            // if the process exits very quickly
            System.Threading.Thread.Sleep(1000);

            // If we allow any timespan to be max value, it could cause unusual behavior, force max-int max (still a LOT)
            // (int.MaxValue ms is about 24 days).  Large values are effectively timeouts for the outer harness
            if (!p.WaitForExit((int)Math.Min(timeOut.TotalMilliseconds, int.MaxValue)))
            {
                _log.LogError("Waiting for command timed out! Execution may be compromised.");
            }

            return (standardOut.ToString(), standardErr.ToString(), p.ExitCode);
        }

        #endregion
    }
}
