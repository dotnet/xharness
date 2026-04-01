// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.Common;

/// <summary>
/// Emits a structured JSON result block to the console log.
/// Used by both Android and Apple platforms to provide consistent output for humans and automated tooling.
/// </summary>
public static class RunSummaryEmitter
{
    public const string JsonStartMarker = "<<XHARNESS_RESULT_START>>";
    public const string JsonEndMarker = "<<XHARNESS_RESULT_END>>";

    /// <summary>
    /// Emits a structured JSON result block to the log.
    /// </summary>
    public static void EmitRunSummary(
        ILogger logger,
        ExitCode exitCode,
        string platform,
        string? deviceName,
        string? deviceOsVersion,
        string? architecture,
        int? instrumentationExitCode,
        IReadOnlyList<DiagnosticsFile> producedFiles)
    {
        EmitRunSummary(
            message => logger.LogInformation(message),
            exitCode, platform, deviceName, deviceOsVersion, architecture, instrumentationExitCode, producedFiles);
    }

    /// <summary>
    /// Emits a structured JSON result block to the log.
    /// Uses an Action&lt;string&gt; for logging to support different logger abstractions.
    /// </summary>
    public static void EmitRunSummary(
        Action<string> logInfo,
        ExitCode exitCode,
        string platform,
        string? deviceName,
        string? deviceOsVersion,
        string? architecture,
        int? instrumentationExitCode,
        IReadOnlyList<DiagnosticsFile> producedFiles)
    {
        EmitJsonResultBlock(logInfo, exitCode, platform, deviceName, deviceOsVersion, architecture, instrumentationExitCode, producedFiles);
    }

    /// <summary>
    /// Emits a machine-readable JSON block between well-known delimiters for AI agents.
    /// When running in Helix, includes API URLs for direct file download.
    /// </summary>
    public static void EmitJsonResultBlock(
        Action<string> logInfo,
        ExitCode exitCode,
        string platform,
        string? deviceName,
        string? deviceOsVersion,
        string? architecture,
        int? instrumentationExitCode,
        IReadOnlyList<DiagnosticsFile> producedFiles)
    {
        var resultData = BuildResultData(exitCode, platform, deviceName, deviceOsVersion, architecture, instrumentationExitCode, producedFiles);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        string json = JsonSerializer.Serialize(resultData, options);
        logInfo($"{JsonStartMarker}{Environment.NewLine}{json}{Environment.NewLine}{JsonEndMarker}");
    }

    /// <summary>
    /// Writes the JSON result block as a file (xharness-result.json) in the specified directory.
    /// This file gets uploaded to Helix automatically when written to the output/uploads directory.
    /// </summary>
    public static void WriteResultJsonFile(
        string outputDirectory,
        ExitCode exitCode,
        string platform,
        string? deviceName,
        string? deviceOsVersion,
        string? architecture,
        int? instrumentationExitCode,
        IReadOnlyList<DiagnosticsFile> producedFiles)
    {
        try
        {
            var resultData = BuildResultData(exitCode, platform, deviceName, deviceOsVersion, architecture, instrumentationExitCode, producedFiles);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };

            string json = JsonSerializer.Serialize(resultData, options);
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(Path.Combine(outputDirectory, "xharness-result.json"), json);
        }
        catch
        {
            // Best effort — don't fail the run if file writing fails
        }
    }

    private static Dictionary<string, object?> BuildResultData(
        ExitCode exitCode,
        string platform,
        string? deviceName,
        string? deviceOsVersion,
        string? architecture,
        int? instrumentationExitCode,
        IReadOnlyList<DiagnosticsFile> producedFiles)
    {
        string? helixJobId = Environment.GetEnvironmentVariable("HELIX_CORRELATION_ID");
        string? helixWorkItem = Environment.GetEnvironmentVariable("HELIX_WORKITEM_FRIENDLYNAME");

        var fileEntries = new List<object>();
        foreach (var file in producedFiles)
        {
            var entry = new Dictionary<string, string>
            {
                ["name"] = file.Name,
                ["type"] = file.Type,
            };

            fileEntries.Add(entry);
        }

        var resultData = new Dictionary<string, object?>
        {
            ["version"] = 1,
            ["machineName"] = Environment.MachineName,
            ["exitCode"] = (int)exitCode,
            ["exitCodeName"] = exitCode.ToString(),
            ["platform"] = platform,
        };

        if (!string.IsNullOrEmpty(helixJobId) && !string.IsNullOrEmpty(helixWorkItem))
        {
            var encodedWorkItem = Uri.EscapeDataString(helixWorkItem);
            resultData["helixWorkItemId"] = helixWorkItem;
            resultData["helixJobId"] = helixJobId;
            resultData["helixConsoleUri"] = $"https://helix.dot.net/api/2019-06-17/jobs/{helixJobId}/workitems/{encodedWorkItem}/console";
            resultData["helixFilesUri"] = $"https://helix.dot.net/api/2019-06-17/jobs/{helixJobId}/workitems/{encodedWorkItem}/files";
        }

        if (instrumentationExitCode.HasValue)
        {
            resultData["instrumentationExitCode"] = instrumentationExitCode.Value;
        }

        if (!string.IsNullOrEmpty(deviceName))
        {
            resultData["device"] = deviceName;
        }

        if (!string.IsNullOrEmpty(deviceOsVersion))
        {
            resultData["deviceOsVersion"] = deviceOsVersion;
        }

        if (!string.IsNullOrEmpty(architecture))
        {
            resultData["architecture"] = architecture;
        }

        if (fileEntries.Count > 0)
        {
            resultData["files"] = fileEntries;
        }

        return resultData;
    }
}
