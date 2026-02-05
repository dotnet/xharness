// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.DotNet.XHarness.Android.Execution;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.Android;

public class EmulatorManager
{
    private readonly ILogger _log;
    private readonly AdbRunner _adbRunner;

    public EmulatorManager(ILogger log, AdbRunner adbRunner)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _adbRunner = adbRunner ?? throw new ArgumentNullException(nameof(adbRunner));
    }

    /// <summary>
    /// Lists available AVDs by invoking the emulator binary and parsing local config.ini files.
    /// </summary>
    public IReadOnlyCollection<AvdInfo> ListAvds()
    {
        var avdHome = GetAvdHome();

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in TryListViaEmulatorBinary())
        {
            names.Add(name);
        }

        foreach (var name in EnumerateNamesFromConfigs(avdHome))
        {
            names.Add(name);
        }

        var result = new List<AvdInfo>();
        foreach (var name in names)
        {
            var configPath = Path.Combine(avdHome, $"{name}.avd", "config.ini");
            int? apiLevel = null;
            string? systemImagePath = null;
            string? architecture = null;

            if (File.Exists(configPath))
            {
                ParseConfig(configPath, out apiLevel, out systemImagePath, out architecture);
            }
            else
            {
                _log.LogDebug($"AVD '{name}' missing config at {configPath}");
            }

            result.Add(new AvdInfo(name, configPath, systemImagePath, apiLevel, architecture));
        }

        return result;
    }

    /// <summary>
    /// Selects an AVD that matches the requested API level (and optional architecture) exactly. No AVDs are created.
    /// </summary>
    public AvdInfo? SelectAvdByApiLevel(int requiredApiLevel, string? requiredArchitecture = null)
    {
        var archDescription = requiredArchitecture != null ? $" and arch {requiredArchitecture}" : string.Empty;
        _log.LogInformation($"Starting Select AVD by API level (API level {requiredApiLevel}{archDescription})");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var avds = ListAvds();
            if (avds.Count == 0)
            {
                _log.LogWarning("No AVDs found while selecting by API level");
                return null;
            }

            var candidates = avds
                .Where(avd => avd.ApiLevel == requiredApiLevel)
                .Where(avd => requiredArchitecture == null || string.Equals(avd.Architecture, requiredArchitecture, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var selected = candidates.FirstOrDefault();
            if (selected == null)
            {
                LogNoMatchWarning(avds, requiredApiLevel, requiredArchitecture);
                return null;
            }

            if (candidates.Count > 1)
            {
                _log.LogWarning($"Multiple AVDs found with API level {requiredApiLevel}{archDescription}; selecting '{selected.Name}'. All candidates: {string.Join(", ", candidates.Select(a => a.Name))}");
            }

            var selectedArch = selected.Architecture ?? "unknown-arch";
            _log.LogInformation($"Selected AVD '{selected.Name}' (API {selected.ApiLevel}, Arch {selectedArch}) for requested API level {requiredApiLevel}{archDescription}");
            return selected;
        }
        finally
        {
            _log.LogInformation($"Finished Select AVD by API level in {stopwatch.Elapsed}");
        }
    }

    private void LogNoMatchWarning(IReadOnlyCollection<AvdInfo> avds, int requiredApiLevel, string? requiredArchitecture)
    {
        var matchingApi = avds.Where(avd => avd.ApiLevel == requiredApiLevel).ToList();

        if (matchingApi.Count == 0)
        {
            var knownApiAvds = avds.Where(avd => avd.ApiLevel.HasValue).ToList();
            var available = knownApiAvds.Count == 0 ? "none" : string.Join(", ", knownApiAvds.Select(DescribeAvd));
            _log.LogWarning($"No AVD found with API level {requiredApiLevel}; available API levels: {available}");
        }
        else
        {
            var available = string.Join(", ", matchingApi.Select(DescribeAvd));
            var archInfo = requiredArchitecture != null ? $" and arch {requiredArchitecture}" : string.Empty;
            _log.LogWarning($"No AVD found with API level {requiredApiLevel}{archInfo}; available for that API level: {available}");
        }
    }

    /// <summary>
    /// Starts an emulator with the specified AVD.
    /// </summary>
    /// <param name="avdName">The name of the AVD to start.</param>
    /// <param name="wipeData">If true, wipes emulator data before starting (default: true).</param>
    /// <returns>True if the emulator started successfully; false otherwise.</returns>
    public bool StartEmulator(string avdName, bool wipeData = true)
    {
        if (string.IsNullOrWhiteSpace(avdName))
        {
            throw new ArgumentException("AVD name cannot be null or whitespace", nameof(avdName));
        }

        var wipeDescription = wipeData ? " and wipe data" : string.Empty;
        _log.LogInformation($"Starting Start Emulator '{avdName}'{wipeDescription}");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var emulatorPath = GetEmulatorPath();
            if (emulatorPath == null)
            {
                _log.LogError("Emulator binary not found; cannot start emulator");
                return false;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = emulatorPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = false,
            };

            startInfo.ArgumentList.Add("-avd");
            startInfo.ArgumentList.Add(avdName);
            startInfo.ArgumentList.Add("-no-window");
            startInfo.ArgumentList.Add("-no-audio");

            if (wipeData)
            {
                startInfo.ArgumentList.Add("-wipe-data");
            }

            var commandStr = $"{emulatorPath} {string.Join(" ", startInfo.ArgumentList)}";
            _log.LogInformation($"Starting emulator process (command: {commandStr})");

            var proc = Process.Start(startInfo);
            if (proc == null)
            {
                _log.LogError("Failed to start emulator process");
                return false;
            }

            _log.LogInformation($"Emulator process started with PID {proc.Id}");
            _log.LogInformation($"Finished Start Emulator in {stopwatch.Elapsed}");
            return true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, $"Error starting emulator '{avdName}'");
            _log.LogInformation($"Failed Start Emulator in {stopwatch.Elapsed}");
            return false;
        }
    }

    /// <summary>
    /// Stops all running emulator processes using ADB.
    /// </summary>
    /// <returns>True if all emulators were stopped successfully; false otherwise.</returns>
    public bool StopAllEmulators()
    {
        _log.LogInformation("Starting Stop All Emulators");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var emulatorPath = GetEmulatorPath();
            if (emulatorPath == null)
            {
                _log.LogWarning("Emulator binary not found; cannot determine running emulators via adb");
                _log.LogInformation($"Finished Stop All Emulators in {stopwatch.Elapsed}");
                return false;
            }

            var emulatorSerials = GetRunningEmulators();
            if (emulatorSerials == null)
            {
                _log.LogInformation($"Failed Stop All Emulators in {stopwatch.Elapsed}");
                return false;
            }

            if (emulatorSerials.Count == 0)
            {
                _log.LogInformation("No running emulators found");
                _log.LogInformation($"Finished Stop All Emulators in {stopwatch.Elapsed}");
                return true;
            }

            _log.LogInformation($"Found {emulatorSerials.Count} running emulator(s): {string.Join(", ", emulatorSerials.Select(s => $"\"{s}\""))}");

            var allStopped = StopEmulators(emulatorSerials);

            if (allStopped)
            {
                _log.LogInformation($"Finished Stop All Emulators in {stopwatch.Elapsed}");
            }
            else
            {
                _log.LogWarning($"Failed to stop some emulators in {stopwatch.Elapsed}");
            }

            return allStopped && VerifyNoEmulatorsRunning();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error stopping emulators");
            _log.LogInformation($"Failed Stop All Emulators in {stopwatch.Elapsed}");
            return false;
        }

        List<string>? GetRunningEmulators()
        {
            var devices = _adbRunner.GetDevices(retries: 0);
            return devices
                .Where(d => d.DeviceSerial.StartsWith("emulator-", StringComparison.OrdinalIgnoreCase))
                .Select(d => $"{d.DeviceSerial}\tdevice")
                .ToList();
        }

        bool StopEmulators(List<string> emulators)
        {
            var allStopped = true;

            foreach (var serial in emulators)
            {
                try
                {
                    var serialId = serial.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)[0];
                    _log.LogInformation($"Stopping emulator {serial}");
                    var killResult = _adbRunner.RunAdbCommand(
                        new[] { "-s", serialId, "emu", "kill" },
                        TimeSpan.FromSeconds(30));

                    if (killResult.ExitCode != 0)
                    {
                        _log.LogWarning($"Failed to stop emulator {serial}: {killResult.StandardError}");
                        allStopped = false;
                    }
                    else
                    {
                        _log.LogInformation($"Emulator {serial} stopped");
                    }
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, $"Failed to stop emulator {serial}");
                    allStopped = false;
                }
            }

            return allStopped;
        }

        bool VerifyNoEmulatorsRunning()
        {
            _log.LogInformation("Verifying all emulators stopped...");
            
            try
            {
                var devices = _adbRunner.GetDevices(retries: 0);
                var remainingEmulators = devices
                    .Where(d => d.DeviceSerial.StartsWith("emulator-", StringComparison.OrdinalIgnoreCase))
                    .Select(d => $"{d.DeviceSerial}\tdevice")
                    .ToList();

                if (remainingEmulators.Count > 0)
                {
                    _log.LogError($"Sanity check failed: {remainingEmulators.Count} emulator(s) still running: {string.Join(", ", remainingEmulators.Select(s => $"\"{s}\""))}");
                    return false;
                }

                _log.LogInformation("Sanity check passed: no emulators running");
                return true;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to verify emulator state");
                return false;
            }
        }
    }

    private IEnumerable<string> TryListViaEmulatorBinary()
    {
        var emulatorPath = GetEmulatorPath();

        if (emulatorPath == null)
        {
            _log.LogDebug("Emulator binary not found; falling back to config enumeration");
            return Enumerable.Empty<string>();
        }

        _log.LogInformation($"Starting List AVDs... (command: {emulatorPath} -list-avds)");

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = emulatorPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("-list-avds");

            using var proc = Process.Start(startInfo);
            if (proc == null)
            {
                _log.LogWarning("Failed to start emulator process for listing AVDs");
                return Enumerable.Empty<string>();
            }

            var output = proc.StandardOutput.ReadToEnd();
            var error = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            _log.LogInformation($"Finished List AVDs (exit {proc.ExitCode})");

            if (proc.ExitCode != 0)
            {
                _log.LogWarning($"Emulator -list-avds failed: {error.Trim()}");
                return Enumerable.Empty<string>();
            }

            return output
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error running emulator -list-avds");
            return Enumerable.Empty<string>();
        }
    }

    private static IEnumerable<string> EnumerateNamesFromConfigs(string avdHome)
    {
        if (!Directory.Exists(avdHome))
        {
            return Enumerable.Empty<string>();
        }

        return Directory.EnumerateDirectories(avdHome, "*.avd", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => name != null)
            .Select(name => name!.Substring(0, name.Length - ".avd".Length));
    }

    private static void ParseConfig(string configPath, out int? apiLevel, out string? systemImagePath, out string? architecture)
    {
        apiLevel = null;
        systemImagePath = null;
        architecture = null;

        foreach (var line in File.ReadLines(configPath))
        {
            if (line.StartsWith("abi.type", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split('=');
                if (parts.Length == 2)
                {
                    architecture ??= NormalizeArchitecture(parts[1]);
                }
                continue;
            }

            if (line.StartsWith("hw.cpu.arch", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split('=');
                if (parts.Length == 2)
                {
                    architecture ??= NormalizeArchitecture(parts[1]);
                }
                continue;
            }

            if (line.StartsWith("image.sysdir.", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split('=');
                if (parts.Length == 2)
                {
                    systemImagePath = parts[1].Trim();
                    var match = Regex.Match(systemImagePath, "android-(\\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out var parsed))
                    {
                        apiLevel = parsed;
                    }

                    if (architecture == null)
                    {
                        architecture = NormalizeArchitecture(InferArchitectureFromPath(systemImagePath));
                    }
                }
            }
        }
    }

    private static string? InferArchitectureFromPath(string? systemImagePath)
    {
        if (string.IsNullOrEmpty(systemImagePath))
        {
            return null;
        }

        var lower = systemImagePath.ToLowerInvariant();

        if (lower.Contains("x86_64"))
        {
            return "x86_64";
        }

        if (lower.Contains("x86"))
        {
            return "x86";
        }

        if (lower.Contains("arm64") || lower.Contains("aarch64"))
        {
            return "arm64";
        }

        if (lower.Contains("armeabi") || lower.Contains("armv7"))
        {
            return "arm";
        }

        return null;
    }

    private static string? NormalizeArchitecture(string? architecture)
    {
        if (string.IsNullOrWhiteSpace(architecture))
        {
            return null;
        }

        var value = architecture.Trim();
        var lower = value.ToLowerInvariant();

        if (lower.Contains("x86_64"))
        {
            return "x86_64";
        }

        if (lower == "x86" || lower.Contains("x86"))
        {
            return "x86";
        }

        if (lower.Contains("arm64") || lower.Contains("aarch64") || lower.Contains("arm64-v8a"))
        {
            return "arm64";
        }

        if (lower.Contains("armeabi") || lower.Contains("armv7"))
        {
            return "arm";
        }

        return value;
    }

    private static string DescribeAvd(AvdInfo avd)
    {
        var api = avd.ApiLevel?.ToString() ?? "unknown-api";
        var arch = avd.Architecture ?? "unknown-arch";
        return $"{avd.Name}:{api}/{arch}";
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

    private string? GetEmulatorPath()
    {
        // Try to find emulator relative to bundled adb (for XHarness runtime bundles)
        var adbPath = _adbRunner.AdbExePath;
        if (!string.IsNullOrEmpty(adbPath))
        {
            var adbDir = Path.GetDirectoryName(adbPath);
            if (!string.IsNullOrEmpty(adbDir))
            {
                // Check if emulator is in sibling directory: runtimes/any/native/adb/linux/adb -> runtimes/any/native/emulator/linux/emulator
                var runtimesDir = Path.GetDirectoryName(Path.GetDirectoryName(adbDir));
                if (!string.IsNullOrEmpty(runtimesDir))
                {
                    var emulatorCandidate = Path.Combine(runtimesDir, "emulator", Path.GetFileName(adbDir), "emulator");
                    if (File.Exists(emulatorCandidate))
                    {
                        _log.LogDebug($"Found emulator relative to bundled adb: {emulatorCandidate}");
                        return emulatorCandidate;
                    }
                }

                // Check if adb and emulator are in the same SDK structure: sdk/platform-tools/adb -> sdk/emulator/emulator
                var sdkRoot = Path.GetDirectoryName(adbDir);
                if (!string.IsNullOrEmpty(sdkRoot))
                {
                    var emulatorCandidate = Path.Combine(sdkRoot, "emulator", "emulator");
                    if (File.Exists(emulatorCandidate))
                    {
                        _log.LogDebug($"Found emulator in SDK structure relative to adb: {emulatorCandidate}");
                        return emulatorCandidate;
                    }
                }
            }
        }

        // Try environment variables
        var envSdkRoot = Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT") ?? Environment.GetEnvironmentVariable("ANDROID_HOME");
        if (!string.IsNullOrEmpty(envSdkRoot))
        {
            var candidate = Path.Combine(envSdkRoot, "emulator", "emulator");
            if (File.Exists(candidate))
            {
                _log.LogDebug($"Found emulator via ANDROID_SDK_ROOT: {candidate}");
                return candidate;
            }
        }

        // Try common Linux installation paths (for Helix and CI environments)
        var commonPaths = new[]
        {
            "/usr/local/android-sdk/emulator/emulator",
            "/usr/local/lib/android/sdk/emulator/emulator",
            "/usr/lib/android-sdk/emulator/emulator",
            "/opt/android-sdk/emulator/emulator",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Android/Sdk/emulator/emulator"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".android-sdk/emulator/emulator"),
        };

        foreach (var path in commonPaths)
        {
            if (File.Exists(path))
            {
                _log.LogDebug($"Found emulator at common path: {path}");
                return path;
            }
        }

        // Fallback to PATH resolution
        _log.LogDebug("Falling back to PATH resolution for emulator");
        return "emulator";
    }
}
