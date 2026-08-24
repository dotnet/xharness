// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.XHarness.iOS.Shared;

public sealed class AppleLaunchDiagnostics
{
    public string BundleId { get; set; } = string.Empty;
    public int? LauncherExitCode { get; set; }
    public int? AppExitCode { get; set; }
    public bool? TestProtocolStarted { get; set; }
    public bool TestEndSignalDetected { get; set; }
    public AppleTestResultFileDiagnostics TestResultFile { get; set; } = new();
    public AppleCrashReportDiagnostics CrashReport { get; set; } = new();
}

public sealed class AppleTestResultFileDiagnostics
{
    public string Path { get; set; } = string.Empty;
    public int CopyAttempts { get; set; }
    public bool Exists { get; set; }
}

public sealed class AppleCrashReportDiagnostics
{
    public int ReportsBeforeLaunch { get; set; }
    public int ReportsCreatedDuringRun { get; set; }
    public AppleCrashReportMetadata? MatchedReport { get; set; }
}

public sealed class AppleCrashReportMetadata
{
    public string Name { get; set; } = string.Empty;
    public string? BugType { get; set; }
    public string? BundleId { get; set; }
    public string? ProcessName { get; set; }
    public int? ProcessId { get; set; }
    public string? Timestamp { get; set; }
    public string MatchReason { get; set; } = string.Empty;
}
