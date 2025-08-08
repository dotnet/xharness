// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Logging;

public interface IDeviceLogCapturer : IDisposable
{
    void StartCapture();
    void StopCapture();
}

public class DeviceLogCapturer : IDeviceLogCapturer
{
    // TODO: Remove process manager
    private readonly IMlaunchProcessManager _processManager;
    private readonly ILog _mainLog;
    private readonly ILog _deviceLog;
    private readonly string _deviceUdid;
    private readonly string _bundleIdentifier;
    private readonly string _outputPath;
    private DateTime _startTime;

    public DeviceLogCapturer(IMlaunchProcessManager processManager, ILog mainLog, ILog deviceLog, string deviceUdid, string bundleIdentifier)
    {
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        _mainLog = mainLog ?? throw new ArgumentNullException(nameof(mainLog));
        _deviceLog = deviceLog ?? throw new ArgumentNullException(nameof(deviceLog));
        _deviceUdid = deviceUdid ?? throw new ArgumentNullException(nameof(deviceUdid));
        _bundleIdentifier = bundleIdentifier;

        _outputPath = Path.Combine(Path.GetTempPath(), $"device_logs_{Guid.NewGuid()}.logarchive");
    }

    public void StartCapture()
    {
        _startTime = DateTime.Now;
        _deviceLog.WriteLine($"Device log capture started at {_startTime:yyyy-MM-dd HH:mm:ss}");
    }

    public void StopCapture()
    {
        _deviceLog.WriteLine($"Device log capture stopped at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        string startTimeStr = _startTime.ToString("yyyy-MM-dd HH:mm:ss");

        // Collect logs
        string collectArguments = $"log collect --device-udid {_deviceUdid} --start \"{startTimeStr}\" --output \"{_outputPath}\"";
        _deviceLog.WriteLine($"Collecting logs: sudo {collectArguments}");

        using Process collectProcess = new Process();
        collectProcess.StartInfo.FileName = "sudo";
        collectProcess.StartInfo.Arguments = collectArguments;
        collectProcess.StartInfo.UseShellExecute = false;
        collectProcess.StartInfo.RedirectStandardOutput = true;
        collectProcess.StartInfo.RedirectStandardError = true;

        StringBuilder collectOutput = new StringBuilder();
        StringBuilder collectErrors = new StringBuilder();

        collectProcess.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                collectOutput.AppendLine(e.Data);
        };

        collectProcess.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                collectErrors.AppendLine(e.Data);
        };

        collectProcess.Start();
        collectProcess.BeginOutputReadLine();
        collectProcess.BeginErrorReadLine();
        collectProcess.WaitForExit();

        if (collectErrors.Length > 0)
        {
            _mainLog.WriteLine($"Errors during log collection: {collectErrors}");
        }

        // Read the collected logs
        string readArguments = $"show \"{_outputPath}\"";
        _deviceLog.WriteLine($"Reading logs: log {readArguments}");

        using Process readProcess = new Process();
        readProcess.StartInfo.FileName = "log";
        readProcess.StartInfo.Arguments = readArguments;
        readProcess.StartInfo.UseShellExecute = false;
        readProcess.StartInfo.RedirectStandardOutput = true;
        readProcess.StartInfo.RedirectStandardError = true;

        StringBuilder output = new StringBuilder();
        StringBuilder errors = new StringBuilder();

        readProcess.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                output.AppendLine(e.Data);
        };

        readProcess.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                errors.AppendLine(e.Data);
        };

        readProcess.Start();
        readProcess.BeginOutputReadLine();
        readProcess.BeginErrorReadLine();
        readProcess.WaitForExit();

        if (output.Length > 0)
        {
            lock (_deviceLog)
            {
                _deviceLog.WriteLine(output.ToString());
            }
        }

        if (errors.Length > 0)
        {
            _mainLog.WriteLine($"Errors while reading device logs: {errors}");
        }

        Directory.Delete(_outputPath, true);
    }

    public void Dispose() => StopCapture();
}

