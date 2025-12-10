// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.Android;

internal class EmulatorDiagnostics
{
    private readonly ILogger _log;

    public EmulatorDiagnostics(ILogger log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>
    /// Collects disk space diagnostics for relevant paths (AVD home, Android SDK, temp directory).
    /// </summary>
    /// <returns>A dictionary mapping path descriptions to available space information.</returns>
    public Dictionary<string, string> CollectDiskSpaceDiagnostics()
    {
        _log.LogInformation("Starting Collect Disk Space Diagnostics");
        var stopwatch = Stopwatch.StartNew();

        var diagnostics = new Dictionary<string, string>();

        try
        {
            // Check AVD home directory
            var avdHome = GetAvdHome();
            AddDiskSpaceInfo(diagnostics, "AVD Home", avdHome);

            // Check Android SDK root
            var sdkRoot = Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT") ?? Environment.GetEnvironmentVariable("ANDROID_HOME");
            if (!string.IsNullOrEmpty(sdkRoot))
            {
                AddDiskSpaceInfo(diagnostics, "Android SDK", sdkRoot);
            }

            // Check temp directory
            var tempPath = Path.GetTempPath();
            AddDiskSpaceInfo(diagnostics, "Temp Directory", tempPath);

            // Check user profile directory
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            AddDiskSpaceInfo(diagnostics, "User Profile", userProfile);

            _log.LogInformation($"Finished Collect Disk Space Diagnostics in {stopwatch.Elapsed}");
            return diagnostics;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error collecting disk space diagnostics");
            diagnostics["Error"] = ex.Message;
            _log.LogInformation($"Failed Collect Disk Space Diagnostics in {stopwatch.Elapsed}");
            return diagnostics;
        }
    }

    private void AddDiskSpaceInfo(Dictionary<string, string> diagnostics, string label, string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                diagnostics[label] = $"{path} (does not exist)";
                return;
            }

            var driveInfo = new DriveInfo(Path.GetPathRoot(path)!);
            var availableGB = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
            var totalGB = driveInfo.TotalSize / (1024.0 * 1024.0 * 1024.0);
            var percentFree = (driveInfo.AvailableFreeSpace * 100.0) / driveInfo.TotalSize;

            diagnostics[label] = $"{path} ({availableGB:F2} GB free of {totalGB:F2} GB, {percentFree:F1}% available)";
            
            if (availableGB < 1.0)
            {
                _log.LogWarning($"Low disk space on {label}: only {availableGB:F2} GB available");
            }
        }
        catch (Exception ex)
        {
            diagnostics[label] = $"{path} (error: {ex.Message})";
            _log.LogDebug(ex, $"Failed to get disk space for {label} at {path}");
        }
    }

    private static string GetAvdHome()
    {
        var avdHome = Environment.GetEnvironmentVariable("ANDROID_AVD_HOME");
        if (!string.IsNullOrEmpty(avdHome))
        {
            return avdHome;
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".android", "avd");
    }

    /// <summary>
    /// Collects memory diagnostics including total RAM, available RAM, and top 5 memory-consuming processes.
    /// </summary>
    /// <returns>A dictionary with memory information and top processes.</returns>
    public Dictionary<string, string> CollectMemoryAndProcessDiagnostics()
    {
        _log.LogInformation("Starting Collect Memory and Process Diagnostics");
        var stopwatch = Stopwatch.StartNew();

        var diagnostics = new Dictionary<string, string>();

        try
        {
            // Get total physical memory
            var totalMemoryMB = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024.0 * 1024.0);
            diagnostics["Total Physical Memory"] = $"{totalMemoryMB:F2} MB";

            // Get current process memory usage
            using var currentProcess = Process.GetCurrentProcess();
            var workingSetMB = currentProcess.WorkingSet64 / (1024.0 * 1024.0);
            diagnostics["XHarness Process Memory"] = $"{workingSetMB:F2} MB";

            // Get top 5 processes by memory usage
            var topProcesses = GetTopProcessesByMemory(5);
            for (int i = 0; i < topProcesses.Count; i++)
            {
                diagnostics[$"Top Process #{i + 1}"] = topProcesses[i];
            }

            _log.LogInformation($"Finished Collect Memory and Process Diagnostics in {stopwatch.Elapsed}");
            return diagnostics;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error collecting memory and process diagnostics");
            diagnostics["Error"] = ex.Message;
            _log.LogInformation($"Failed Collect Memory and Process Diagnostics in {stopwatch.Elapsed}");
            return diagnostics;
        }
    }

    private List<string> GetTopProcessesByMemory(int count)
    {
        var topProcesses = new List<string>();

        try
        {
            var processes = Process.GetProcesses()
                .Where(p => !string.IsNullOrEmpty(p.ProcessName))
                .OrderByDescending(p =>
                {
                    try
                    {
                        return p.WorkingSet64;
                    }
                    catch
                    {
                        return 0;
                    }
                })
                .Take(count)
                .ToList();

            foreach (var proc in processes)
            {
                try
                {
                    var memoryMB = proc.WorkingSet64 / (1024.0 * 1024.0);
                    topProcesses.Add($"{proc.ProcessName} (PID: {proc.Id}, Memory: {memoryMB:F2} MB)");
                }
                catch (Exception ex)
                {
                    _log.LogDebug(ex, $"Failed to get info for process {proc.ProcessName}");
                }
                finally
                {
                    proc.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "Error enumerating processes");
            topProcesses.Add($"Error: {ex.Message}");
        }

        return topProcesses;
    }

    /// <summary>
    /// Collects running emulator process information (PIDs, command lines).
    /// </summary>
    public Dictionary<string, string> CollectRunningEmulatorProcesses()
    {
        _log.LogInformation("Starting Collect Running Emulator Processes");
        var stopwatch = Stopwatch.StartNew();

        var diagnostics = new Dictionary<string, string>();

        try
        {
            var emulatorProcesses = Process.GetProcesses()
                .Where(p => p.ProcessName.Contains("emulator", StringComparison.OrdinalIgnoreCase) ||
                           p.ProcessName.Contains("qemu", StringComparison.OrdinalIgnoreCase))
                .ToList();

            diagnostics["Emulator Process Count"] = emulatorProcesses.Count.ToString();

            for (int i = 0; i < emulatorProcesses.Count; i++)
            {
                var proc = emulatorProcesses[i];
                try
                {
                    var memoryMB = proc.WorkingSet64 / (1024.0 * 1024.0);
                    var startTime = proc.StartTime.ToString("yyyy-MM-dd HH:mm:ss");
                    
                    diagnostics[$"Emulator Process #{i + 1}"] = 
                        $"{proc.ProcessName} (PID: {proc.Id}, Started: {startTime}, Memory: {memoryMB:F2} MB)";
                }
                catch (Exception ex)
                {
                    diagnostics[$"Emulator Process #{i + 1}"] = $"{proc.ProcessName} (PID: {proc.Id}, error: {ex.Message})";
                    _log.LogDebug(ex, $"Failed to get full info for emulator process {proc.ProcessName}");
                }
                finally
                {
                    proc.Dispose();
                }
            }

            if (emulatorProcesses.Count == 0)
            {
                diagnostics["Status"] = "No emulator processes found";
            }

            _log.LogInformation($"Finished Collect Running Emulator Processes in {stopwatch.Elapsed}");
            return diagnostics;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error collecting running emulator processes");
            diagnostics["Error"] = ex.Message;
            _log.LogInformation($"Failed Collect Running Emulator Processes in {stopwatch.Elapsed}");
            return diagnostics;
        }
    }

    /// <summary>
    /// Validates AVD configuration for the specified AVD name.
    /// </summary>
    public Dictionary<string, string> ValidateAvdConfig(string avdName)
    {
        _log.LogInformation($"Starting Validate AVD Config for '{avdName}'");
        var stopwatch = Stopwatch.StartNew();

        var diagnostics = new Dictionary<string, string>();

        try
        {
            var avdHome = GetAvdHome();
            var avdPath = Path.Combine(avdHome, $"{avdName}.avd");
            var configPath = Path.Combine(avdPath, "config.ini");

            diagnostics["AVD Name"] = avdName;
            diagnostics["AVD Path"] = avdPath;
            diagnostics["Config Path"] = configPath;

            if (!Directory.Exists(avdPath))
            {
                diagnostics["AVD Directory Exists"] = "No - AVD directory not found";
                diagnostics["Status"] = "Invalid - AVD directory missing";
                _log.LogWarning($"AVD directory does not exist: {avdPath}");
                _log.LogInformation($"Finished Validate AVD Config in {stopwatch.Elapsed}");
                return diagnostics;
            }

            diagnostics["AVD Directory Exists"] = "Yes";

            if (!File.Exists(configPath))
            {
                diagnostics["Config File Exists"] = "No - config.ini not found";
                diagnostics["Status"] = "Invalid - config.ini missing";
                _log.LogWarning($"config.ini does not exist: {configPath}");
                _log.LogInformation($"Finished Validate AVD Config in {stopwatch.Elapsed}");
                return diagnostics;
            }

            diagnostics["Config File Exists"] = "Yes";

            // Check if config.ini is readable and has content
            var configContent = File.ReadAllText(configPath);
            diagnostics["Config File Size"] = $"{configContent.Length} bytes";

            if (string.IsNullOrWhiteSpace(configContent))
            {
                diagnostics["Config File Content"] = "Empty or whitespace only";
                diagnostics["Status"] = "Invalid - config.ini is empty";
                _log.LogWarning($"config.ini is empty: {configPath}");
            }
            else
            {
                diagnostics["Config File Content"] = $"{configContent.Split('\n').Length} lines";

                // Check for essential config keys
                var hasImageSysDir = configContent.Contains("image.sysdir", StringComparison.OrdinalIgnoreCase);
                var hasAbi = configContent.Contains("abi.type", StringComparison.OrdinalIgnoreCase) || 
                            configContent.Contains("hw.cpu.arch", StringComparison.OrdinalIgnoreCase);

                diagnostics["Has System Image Path"] = hasImageSysDir ? "Yes" : "No";
                diagnostics["Has ABI Info"] = hasAbi ? "Yes" : "No";

                if (hasImageSysDir && hasAbi)
                {
                    diagnostics["Status"] = "Valid";
                }
                else
                {
                    diagnostics["Status"] = "Warning - Missing essential config keys";
                    _log.LogWarning($"AVD config missing essential keys (imageSysDir: {hasImageSysDir}, ABI: {hasAbi})");
                }
            }

            _log.LogInformation($"Finished Validate AVD Config in {stopwatch.Elapsed}");
            return diagnostics;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, $"Error validating AVD config for '{avdName}'");
            diagnostics["Error"] = ex.Message;
            diagnostics["Status"] = $"Error - {ex.Message}";
            _log.LogInformation($"Failed Validate AVD Config in {stopwatch.Elapsed}");
            return diagnostics;
        }
    }

    /// <summary>
    /// Collects all boot failure diagnostics.
    /// </summary>
    public void CollectAndLogBootFailureDiagnostics(string avdName)
    {
        _log.LogError($"==== Boot Failure Diagnostics for AVD '{avdName}' ====");

        try
        {
            // 1. Disk space
            _log.LogError("--- Disk Space ---");
            var diskInfo = CollectDiskSpaceDiagnostics();
            foreach (var kvp in diskInfo)
            {
                _log.LogError($"{kvp.Key}: {kvp.Value}");
            }

            // 2. Memory and processes
            _log.LogError("--- Memory and Processes ---");
            var memoryInfo = CollectMemoryAndProcessDiagnostics();
            foreach (var kvp in memoryInfo)
            {
                _log.LogError($"{kvp.Key}: {kvp.Value}");
            }

            // 3. Running emulator processes
            _log.LogError("--- Running Emulator Processes ---");
            var emulatorProcs = CollectRunningEmulatorProcesses();
            foreach (var kvp in emulatorProcs)
            {
                _log.LogError($"{kvp.Key}: {kvp.Value}");
            }

            // 4. AVD configuration
            _log.LogError("--- AVD Configuration ---");
            var avdConfig = ValidateAvdConfig(avdName);
            foreach (var kvp in avdConfig)
            {
                _log.LogError($"{kvp.Key}: {kvp.Value}");
            }

            _log.LogError("==== End Boot Failure Diagnostics ====");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error collecting boot failure diagnostics");
        }
    }
}
