// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.XHarness.Android.Execution;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.Android.Tests;

public class InstrumentationRunnerTests : IDisposable
{
    private static readonly string s_outputDirectory = Path.Combine(Path.GetTempPath(), $"xharness-instrumentation-test-{Guid.NewGuid()}");
    private static readonly string s_adbPath = Path.Combine(Path.GetTempPath(), $"adb-instr-{Guid.NewGuid()}");

    private readonly Mock<ILogger> _mockLogger;
    private readonly List<(LogLevel Level, string Message)> _loggedEntries;
    private readonly Mock<IAdbProcessManager> _processManager;

    // Crash-indicating instrumentation output (no return-code, shortMsg=Process crashed)
    private const string CrashInstrumentationStdout =
        "INSTRUMENTATION_RESULT: shortMsg=Process crashed.\r\n" +
        "INSTRUMENTATION_STATUS: numtests=1\r\n";

    public InstrumentationRunnerTests()
    {
        _mockLogger = new Mock<ILogger>();
        _loggedEntries = new List<(LogLevel, string)>();

        _mockLogger
            .Setup(l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback((LogLevel level, EventId _, object state, Exception? __, Delegate ___)
                => _loggedEntries.Add((level, state?.ToString() ?? string.Empty)));

        _mockLogger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        _processManager = new Mock<IAdbProcessManager>();
        _processManager
            .Setup(pm => pm.Run(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<TimeSpan>()))
            .Returns((string p, IEnumerable<string> a, TimeSpan t) => FakeAdb(p, [.. a], t));

        Directory.CreateDirectory(s_outputDirectory);
        File.WriteAllText(s_adbPath, string.Empty);
    }

    public void Dispose()
    {
        if (Directory.Exists(s_outputDirectory))
            Directory.Delete(s_outputDirectory, recursive: true);
        if (File.Exists(s_adbPath))
            File.Delete(s_adbPath);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void RunApkInstrumentation_AppCrash_LogsCrashDiagnosticPaths()
    {
        const string apkPackageName = "com.example.mytest";
        var runner = new AdbRunner(_mockLogger.Object, _processManager.Object, s_adbPath);
        var instrRunner = new InstrumentationRunner(_mockLogger.Object, runner);

        var exitCode = instrRunner.RunApkInstrumentation(
            apkPackageName: apkPackageName,
            instrumentationName: null,
            instrumentationArguments: new Dictionary<string, string>(),
            outputDirectory: s_outputDirectory,
            deviceOutputFolder: null,
            timeout: TimeSpan.FromMinutes(5),
            expectedExitCode: 0);

        Assert.Equal(ExitCode.APP_CRASH, exitCode);

        // Expect at least one LogLevel.Error message that mentions both logcat and bugreport paths
        var crashDiag = _loggedEntries
            .FirstOrDefault(e =>
                e.Level == LogLevel.Error &&
                e.Message.Contains("logcat") &&
                e.Message.Contains("bugreport"));

        Assert.True(crashDiag != default,
            "Expected a LogLevel.Error message containing both 'logcat' and 'bugreport' path references after an app crash. " +
            $"All logged messages: [{string.Join(" | ", _loggedEntries.Select(e => $"[{e.Level}] {e.Message}"))}]");

        // The message should reference the actual artifact file names so operators can find them
        Assert.Contains($"adb-logcat-{apkPackageName}", crashDiag.Message);
        Assert.Contains($"adb-bugreport-{apkPackageName}", crashDiag.Message);
    }

    private ProcessExecutionResults FakeAdb(string process, string[] args, TimeSpan timeout)
    {
        if (args.Length == 0)
            return new ProcessExecutionResults { ExitCode = 0 };

        // Skip optional "-s <serial>" device selector
        int start = args[0] == "-s" ? 2 : 0;
        string cmd = start < args.Length ? args[start] : string.Empty;

        // "shell am instrument ..." -> return crash stdout
        if (cmd == "shell" && start + 2 < args.Length
            && args[start + 1] == "am" && args[start + 2] == "instrument")
        {
            return new ProcessExecutionResults
            {
                ExitCode = 0,
                StandardOutput = CrashInstrumentationStdout,
            };
        }

        // "shell getprop ro.build.version.sdk" -> API 33 (so NewReportManager is used)
        if (cmd == "shell" && start + 2 < args.Length
            && args[start + 1] == "getprop" && args[start + 2] == "ro.build.version.sdk")
        {
            return new ProcessExecutionResults { ExitCode = 0, StandardOutput = "33\n" };
        }

        // "logcat -d ..." -> return sample logcat so TryDumpAdbLog succeeds
        if (cmd == "logcat")
        {
            return new ProcessExecutionResults { ExitCode = 0, StandardOutput = "sample logcat line" };
        }

        // "bugreport <path>" -> NewReportManager call; exit 0 signals success
        if (cmd == "bugreport")
        {
            return new ProcessExecutionResults { ExitCode = 0 };
        }

        // Everything else: succeed silently
        return new ProcessExecutionResults { ExitCode = 0 };
    }
}
