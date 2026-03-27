// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.Android.Tests;

public class RunSummaryEmitterTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly List<string> _loggedMessages;

    public RunSummaryEmitterTests()
    {
        _mockLogger = new Mock<ILogger>();
        _loggedMessages = new List<string>();

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

    [Fact]
    public void EmitRunSummary_ContainsDelimiters()
    {
        var files = new List<DiagnosticsFile>
        {
            new() { Name = "testResults.xml", Type = "test-results" }
        };

        RunSummaryEmitter.EmitRunSummary(_mockLogger.Object, ExitCode.SUCCESS, "android", "device1", "API 33", "arm64", 0, files);

        var jsonMessage = _loggedMessages.Find(m => m.Contains("<<XHARNESS_RESULT_START>>"));
        Assert.NotNull(jsonMessage);
        Assert.Contains("<<XHARNESS_RESULT_END>>", jsonMessage);
    }

    [Fact]
    public void EmitRunSummary_ContainsHumanSummary()
    {
        RunSummaryEmitter.EmitRunSummary(_mockLogger.Object, ExitCode.TESTS_FAILED, "android", "device1", "API 33", "arm64", 1, new List<DiagnosticsFile>());

        var summary = _loggedMessages.Find(m => m.Contains("XHARNESS RUN SUMMARY"));
        Assert.NotNull(summary);
        Assert.Contains("TESTS_FAILED", summary);
        Assert.Contains("device1", summary);
    }

    [Fact]
    public void EmitJsonResultBlock_ContainsExitCode()
    {
        RunSummaryEmitter.EmitRunSummary(_mockLogger.Object, ExitCode.TESTS_FAILED, "android", null, null, null, 1, new List<DiagnosticsFile>());

        var json = ExtractJsonFromLogs();
        Assert.Equal(1, json.GetProperty("exitCode").GetInt32());
        Assert.Equal("TESTS_FAILED", json.GetProperty("exitCodeName").GetString());
    }

    [Fact]
    public void EmitJsonResultBlock_ContainsPlatform()
    {
        RunSummaryEmitter.EmitRunSummary(_mockLogger.Object, ExitCode.SUCCESS, "apple", "iPhone", "iOS 18", null, null, new List<DiagnosticsFile>());

        var json = ExtractJsonFromLogs();
        Assert.Equal("apple", json.GetProperty("platform").GetString());
    }

    [Fact]
    public void EmitJsonResultBlock_ContainsDeviceInfo()
    {
        RunSummaryEmitter.EmitRunSummary(_mockLogger.Object, ExitCode.SUCCESS, "android", "SERIAL123", "API 33", "arm64-v8a", 0, new List<DiagnosticsFile>());

        var json = ExtractJsonFromLogs();
        Assert.Equal("SERIAL123", json.GetProperty("device").GetString());
        Assert.Equal("API 33", json.GetProperty("deviceOsVersion").GetString());
        Assert.Equal("arm64-v8a", json.GetProperty("architecture").GetString());
    }

    [Fact]
    public void EmitJsonResultBlock_ContainsFileInfo()
    {
        var files = new List<DiagnosticsFile>
        {
            new() { Name = "testResults.xml", Type = "test-results" },
            new() { Name = "logcat.log", Type = "logcat" },
        };

        RunSummaryEmitter.EmitRunSummary(_mockLogger.Object, ExitCode.SUCCESS, "android", null, null, null, 0, files);

        var json = ExtractJsonFromLogs();
        var filesArray = json.GetProperty("files");
        Assert.Equal(2, filesArray.GetArrayLength());
        Assert.Equal("testResults.xml", filesArray[0].GetProperty("name").GetString());
    }

    [Fact]
    public void EmitJsonResultBlock_IncludesHelixUrls_WhenEnvVarsSet()
    {
        var files = new List<DiagnosticsFile>
        {
            new() { Name = "testResults.xml", Type = "test-results" },
        };

        Environment.SetEnvironmentVariable("HELIX_CORRELATION_ID", "test-job-id");
        Environment.SetEnvironmentVariable("HELIX_WORKITEM_FRIENDLYNAME", "My.Test");

        try
        {
            RunSummaryEmitter.EmitRunSummary(_mockLogger.Object, ExitCode.SUCCESS, "android", null, null, null, 0, files);

            var json = ExtractJsonFromLogs();
            var url = json.GetProperty("files")[0].GetProperty("helixApiUrl").GetString();
            Assert.Contains("test-job-id", url);
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
        var files = new List<DiagnosticsFile>
        {
            new() { Name = "testResults.xml", Type = "test-results" },
        };

        Environment.SetEnvironmentVariable("HELIX_CORRELATION_ID", null);
        Environment.SetEnvironmentVariable("HELIX_WORKITEM_FRIENDLYNAME", null);

        RunSummaryEmitter.EmitRunSummary(_mockLogger.Object, ExitCode.SUCCESS, "android", null, null, null, 0, files);

        var json = ExtractJsonFromLogs();
        Assert.False(json.GetProperty("files")[0].TryGetProperty("helixApiUrl", out _));
    }

    [Fact]
    public void EmitJsonResultBlock_HasVersionField()
    {
        RunSummaryEmitter.EmitRunSummary(_mockLogger.Object, ExitCode.SUCCESS, "android", null, null, null, 0, new List<DiagnosticsFile>());

        var json = ExtractJsonFromLogs();
        Assert.Equal(1, json.GetProperty("version").GetInt32());
    }

    private JsonElement ExtractJsonFromLogs()
    {
        var jsonMessage = _loggedMessages.Find(m => m.Contains("<<XHARNESS_RESULT_START>>"));
        Assert.NotNull(jsonMessage);
        var start = jsonMessage.IndexOf("<<XHARNESS_RESULT_START>>") + "<<XHARNESS_RESULT_START>>".Length;
        var end = jsonMessage.IndexOf("<<XHARNESS_RESULT_END>>");
        var jsonStr = jsonMessage[start..end].Trim();
        return JsonDocument.Parse(jsonStr).RootElement;
    }
}
