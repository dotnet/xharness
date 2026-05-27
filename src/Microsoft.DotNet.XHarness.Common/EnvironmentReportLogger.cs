// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Microsoft.DotNet.XHarness.Common;

public static class EnvironmentReportLogger
{
    public static HostEnvironmentInfo GetHostEnvironmentInfo()
        => HostEnvironmentInfoProvider.GetHostEnvironmentInfo();

    public static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

        string[] units = { "B", "KiB", "MiB", "GiB", "TiB" };
        double value = bytes;
        var unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }

    public static string FormatFrequencyHertz(long hertz)
        => hertz <= 0 ? "0 Hz" : FormatFrequencyKiloHertz(hertz / 1000);

    public static string FormatFrequencyKiloHertz(long kiloHertz)
    {
        if (kiloHertz <= 0)
        {
            return "0 kHz";
        }

        if (kiloHertz >= 1_000_000)
        {
            return $"{kiloHertz / 1_000_000d:0.##} GHz";
        }

        if (kiloHertz >= 1_000)
        {
            return $"{kiloHertz / 1_000d:0.##} MHz";
        }

        return $"{kiloHertz.ToString(CultureInfo.InvariantCulture)} kHz";
    }

    private static class HostEnvironmentInfoProvider
    {
        public static HostEnvironmentInfo GetHostEnvironmentInfo()
        {
            return new HostEnvironmentInfo
            {
                MachineName = Environment.MachineName,
                OperatingSystem = RuntimeInformation.OSDescription,
                OperatingSystemArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                FrameworkDescription = RuntimeInformation.FrameworkDescription,
                LogicalProcessorCount = Environment.ProcessorCount,
                CpuModel = GetCpuModel(),
                CpuMaxFrequencyHertz = GetCpuMaxFrequencyHertz(),
                TotalMemoryBytes = GetTotalMemoryBytes(),
            };
        }

        private static string? GetCpuModel()
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    using var cpuKey = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                    return cpuKey?.GetValue("ProcessorNameString")?.ToString()?.Trim();
                }

                if (OperatingSystem.IsLinux())
                {
                    var cpuInfo = ReadFileIfExists("/proc/cpuinfo");
                    return TryGetColonDelimitedValue(cpuInfo, "model name")
                        ?? TryGetColonDelimitedValue(cpuInfo, "Hardware")
                        ?? TryGetColonDelimitedValue(cpuInfo, "Processor");
                }

                if (OperatingSystem.IsMacOS())
                {
                    return RunCommand("sysctl", "-n", "machdep.cpu.brand_string");
                }
            }
            catch
            {
            }

            return null;
        }

        private static long? GetCpuMaxFrequencyHertz()
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    using var cpuKey = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                    object? frequencyValue = cpuKey?.GetValue("~MHz");
                    if (frequencyValue != null && long.TryParse(frequencyValue.ToString(), out var megaHertz))
                    {
                        return megaHertz * 1_000_000;
                    }
                }

                if (OperatingSystem.IsLinux())
                {
                    var cpuFrequency = ReadFirstExistingFile(
                        "/sys/devices/system/cpu/cpu0/cpufreq/cpuinfo_max_freq",
                        "/sys/devices/system/cpu/cpu0/cpufreq/scaling_max_freq");
                    if (TryParseLong(cpuFrequency, out var kiloHertz))
                    {
                        return kiloHertz * 1000;
                    }

                    var cpuInfo = ReadFileIfExists("/proc/cpuinfo");
                    var cpuMhz = TryGetColonDelimitedValue(cpuInfo, "cpu MHz");
                    if (double.TryParse(cpuMhz, NumberStyles.Float, CultureInfo.InvariantCulture, out var megaHertz))
                    {
                        return (long)Math.Round(megaHertz * 1_000_000);
                    }
                }

                if (OperatingSystem.IsMacOS())
                {
                    var frequency = RunCommand("sysctl", "-n", "hw.cpufrequency_max")
                        ?? RunCommand("sysctl", "-n", "hw.cpufrequency");
                    if (TryParseLong(frequency, out var hertz))
                    {
                        return hertz;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static long? GetTotalMemoryBytes()
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    if (GetPhysicallyInstalledSystemMemory(out var kiloBytes))
                    {
                        return kiloBytes * 1024;
                    }
                }

                if (OperatingSystem.IsLinux())
                {
                    var memInfo = ReadFileIfExists("/proc/meminfo");
                    var totalMemory = TryGetColonDelimitedValue(memInfo, "MemTotal");
                    if (TryParseLong(totalMemory, out var kiloBytes))
                    {
                        return kiloBytes * 1024;
                    }
                }

                if (OperatingSystem.IsMacOS())
                {
                    var memory = RunCommand("sysctl", "-n", "hw.memsize");
                    if (TryParseLong(memory, out var bytes))
                    {
                        return bytes;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static string? ReadFirstExistingFile(params string[] paths)
        {
            foreach (var path in paths)
            {
                var content = ReadFileIfExists(path);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    return content;
                }
            }

            return null;
        }

        private static string? ReadFileIfExists(string path)
            => File.Exists(path) ? File.ReadAllText(path).Trim() : null;

        private static string? RunCommand(string fileName, params string[] arguments)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = string.Join(" ", arguments),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            if (!process.WaitForExit(5000))
            {
                try
                {
                    process.Kill(true);
                }
                catch
                {
                }

                return null;
            }

            if (process.ExitCode != 0)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            return string.IsNullOrWhiteSpace(output) ? null : output;
        }

        private static string? TryGetColonDelimitedValue(string? content, string key)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            foreach (var line in content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!line.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var separatorIndex = line.IndexOf(':');
                if (separatorIndex < 0 || separatorIndex == line.Length - 1)
                {
                    continue;
                }

                return line.Substring(separatorIndex + 1).Trim();
            }

            return null;
        }

        private static bool TryParseLong(string? value, out long parsed)
        {
            parsed = 0;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var token = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            return long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
        }

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetPhysicallyInstalledSystemMemory(out long totalMemoryInKilobytes);
    }
}
