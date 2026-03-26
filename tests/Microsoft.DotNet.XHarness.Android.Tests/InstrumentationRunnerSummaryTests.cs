// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.DotNet.XHarness.Android.Execution;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.Android.Tests;

public class InstrumentationRunnerSummaryTests : IDisposable
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<IAdbProcessManager> _processManager;
    private readonly List<string> _loggedMessages;
    private readonly string _fakeAdbPath;
    private readonly string _tempDir;

    public InstrumentationRunnerSummaryTests()
    {
        _mockLogger = new Mock<ILogger>();
        _processManager = new Mock<IAdbProcessManager>();
        _loggedMessages = new List<string>();
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _fakeAdbPath = Path.Combine(_tempDir, "adb");
        File.WriteAllText(_fakeAdbPath, string.Empty);

        _mockLogger
            .Setup(l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback((LogLevel level, EventId eventId, object state, Exception? ex, Delegate formatter) =>
            {
                _loggedMessages.Add(state?.ToString() ?? "");
            });
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void EmitJsonResultBlock_ContainsDelimiters()
    {
        var runner = CreateRunner();
        var files = new List<DiagnosticsFile>
        {
            new() { Name = "testResults.xml", Type = "test-results" }
        };

        runner.EmitJsonResultBlock(ExitCode.SUCCESS, 0, null, files);

        var jsonMessage = _loggedMessages.Find(m => m.Contains("<<XHARNESS_RESULT_START>>"));
        Assert.NotNull(jsonMessage);
        Assert.Contains("<<XHARNESS_RESULT_END>>", jsonMessage);
    }

    [Fact]
    public void EmitJsonResultBlock_ContainsExitCode()
    {
        var runner = CreateRunner();

        runner.EmitJsonResultBlock(ExitCode.TESTS_FAILED, 1, null, new List<DiagnosticsFile>());

        var json = ExtractJsonFromLogs();
        Assert.Equal(1, json.GetProperty("exitCode").GetInt32());
        Assert.Equal("TESTS_FAILED", json.GetProperty("exitCodeName").GetString());
    }

    [Fact]
    public void EmitJsonResultBlock_ContainsDeviceInfo()
    {
        var runner = CreateRunner();
        var device = new AndroidDevice("SERIAL123") { ApiVersion = 33, Architecture = "arm64-v8a" };

        runner.EmitJsonResultBlock(ExitCode.SUCCESS, 0, device, new List<DiagnosticsFile>());

        var json = ExtractJsonFromLogs();
        Assert.Equal("SERIAL123", json.GetProperty("device").GetString());
        Assert.Equal(33, json.GetProperty("apiVersion").GetInt32());
        Assert.Equal("arm64-v8a", json.GetProperty("architecture").GetString());
    }

    [Fact]
    public void EmitJsonResultBlock_ContainsFileInfo()
    {
        var runner = CreateRunner();
        var files = new List<DiagnosticsFile>
        {
            new() { Name = "testResults.xml", Type = "test-results" },
            new() { Name = "logcat.log", Type = "logcat" },
        };

        runner.EmitJsonResultBlock(ExitCode.SUCCESS, 0, null, files);

        var json = ExtractJsonFromLogs();
        var filesArray = json.GetProperty("files");
        Assert.Equal(2, filesArray.GetArrayLength());
        Assert.Equal("testResults.xml", filesArray[0].GetProperty("name").GetString());
        Assert.Equal("test-results", filesArray[0].GetProperty("type").GetString());
    }

    [Fact]
    public void EmitJsonResultBlock_IncludesHelixUrls_WhenEnvVarsSet()
    {
        var runner = CreateRunner();
        var files = new List<DiagnosticsFile>
        {
            new() { Name = "testResults.xml", Type = "test-results" },
        };

        Environment.SetEnvironmentVariable("HELIX_CORRELATION_ID", "test-job-id");
        Environment.SetEnvironmentVariable("HELIX_WORKITEM_FRIENDLYNAME", "My.Test.Work.Item");

        try
        {
            runner.EmitJsonResultBlock(ExitCode.SUCCESS, 0, null, files);

            var json = ExtractJsonFromLogs();
            var fileEntry = json.GetProperty("files")[0];
            var url = fileEntry.GetProperty("helixApiUrl").GetString();
            Assert.Contains("test-job-id", url);
            Assert.Contains("My.Test.Work.Item", url);
            Assert.Contains("testResults.xml", url);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HELIX_CORRELATION_ID", null);
            Environment.SetEnvironmentVariable("HELIX_WORKITEM_FRIENDLYNAME", null);
        }
    }

    [Fact]
    public void EmitJsonResultBlock_OmitsHelixUrls_WhenEnvVarsNotSet()
    {
        var runner = CreateRunner();
        var files = new List<DiagnosticsFile>
        {
            new() { Name = "testResults.xml", Type = "test-results" },
        };

        Environment.SetEnvironmentVariable("HELIX_CORRELATION_ID", null);
        Environment.SetEnvironmentVariable("HELIX_WORKITEM_FRIENDLYNAME", null);

        runner.EmitJsonResultBlock(ExitCode.SUCCESS, 0, null, files);

        var json = ExtractJsonFromLogs();
        var fileEntry = json.GetProperty("files")[0];
        Assert.False(fileEntry.TryGetProperty("helixApiUrl", out _));
    }

    [Fact]
    public void EmitJsonResultBlock_HasVersionField()
    {
        var runner = CreateRunner();

        runner.EmitJsonResultBlock(ExitCode.SUCCESS, 0, null, new List<DiagnosticsFile>());

        var json = ExtractJsonFromLogs();
        Assert.Equal(1, json.GetProperty("version").GetInt32());
    }

    private InstrumentationRunner CreateRunner()
    {
        var adbRunner = new AdbRunner(_mockLogger.Object, _processManager.Object, _fakeAdbPath);
        return new InstrumentationRunner(_mockLogger.Object, adbRunner);
    }

    private JsonElement ExtractJsonFromLogs()
    {
        var jsonMessage = _loggedMessages.Find(m => m.Contains("<<XHARNESS_RESULT_START>>"));
        Assert.NotNull(jsonMessage);
        return ExtractJson(jsonMessage);
    }

    private static JsonElement ExtractJson(string message)
    {
        var start = message.IndexOf("<<XHARNESS_RESULT_START>>") + "<<XHARNESS_RESULT_START>>".Length;
        var end = message.IndexOf("<<XHARNESS_RESULT_END>>");
        var jsonStr = message[start..end].Trim();
        return JsonDocument.Parse(jsonStr).RootElement;
    }
}
