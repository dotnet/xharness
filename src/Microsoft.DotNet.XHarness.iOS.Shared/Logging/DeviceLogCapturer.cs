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

    public DeviceLogCapturer(IMlaunchProcessManager processManager, ILog mainLog, ILog deviceLog, string deviceUdid, string bundleIdentifier = null)
    {
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        _mainLog = mainLog ?? throw new ArgumentNullException(nameof(mainLog));
        _deviceLog = deviceLog ?? throw new ArgumentNullException(nameof(deviceLog));
        _deviceUdid = deviceUdid ?? throw new ArgumentNullException(nameof(deviceUdid));
        _bundleIdentifier = bundleIdentifier;

        // Create a .logarchive path
        var tempDir = Path.GetTempPath();
        _outputPath = Path.Combine(tempDir, $"device_logs_{Guid.NewGuid()}.logarchive");
    }

    private Process _process;

    public void StartCapture()
    {
        // Record the start time for later log collection
        _startTime = DateTime.Now;
        _deviceLog.WriteLine($"Device log capture started at {_startTime:yyyy-MM-dd HH:mm:ss}");
    }

    public void StopCapture()
    {
        var endTime = DateTime.Now;
        _deviceLog.WriteLine($"Device log capture stopped at {endTime:yyyy-MM-dd HH:mm:ss}");

        try
        {
            // Use sudo log collect to get logs from start time to end time
            var startTimeStr = _startTime.ToString("yyyy-MM-dd HH:mm:ss");
            var endTimeStr = endTime.ToString("yyyy-MM-dd HH:mm:ss");

            var arguments = $"log collect --device-udid {_deviceUdid} --start \"{startTimeStr}\" --end \"{endTimeStr}\" --output \"{_outputPath}\"";

            if (!string.IsNullOrEmpty(_bundleIdentifier))
            {
                // Add predicate to filter logs for specific bundle identifier
                arguments += $" --predicate 'process == \"{_bundleIdentifier}\"'";
            }

            _deviceLog.WriteLine($"Collecting logs: sudo {arguments}");

            var collectProcess = new Process();
            collectProcess.StartInfo.FileName = "sudo";
            collectProcess.StartInfo.Arguments = arguments;
            collectProcess.StartInfo.UseShellExecute = false;
            collectProcess.StartInfo.RedirectStandardOutput = true;
            collectProcess.StartInfo.RedirectStandardError = true;

            var collectOutput = new StringBuilder();
            var collectErrors = new StringBuilder();

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

            collectProcess.Dispose();
        }
        catch (Exception ex)
        {
            _mainLog.WriteLine($"Failed to collect device logs: {ex.Message}");
        }

        // Read the collected logs from the output .logarchive
        if (Directory.Exists(_outputPath))
        {
            try
            {
                // Use 'log show' to convert the .logarchive to readable text
                var logShowProcess = new Process();
                logShowProcess.StartInfo.FileName = "log";
                logShowProcess.StartInfo.Arguments = $"show \"{_outputPath}\"";
                logShowProcess.StartInfo.UseShellExecute = false;
                logShowProcess.StartInfo.RedirectStandardOutput = true;
                logShowProcess.StartInfo.RedirectStandardError = true;

                var output = new StringBuilder();
                var errors = new StringBuilder();

                logShowProcess.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                        output.AppendLine(e.Data);
                };

                logShowProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                        errors.AppendLine(e.Data);
                };

                logShowProcess.Start();
                logShowProcess.BeginOutputReadLine();
                logShowProcess.BeginErrorReadLine();
                logShowProcess.WaitForExit();

                if (output.Length > 0)
                {
                    lock (_deviceLog)
                    {
                        _deviceLog.WriteLine("--- Device logs collected ---");
                        _deviceLog.WriteLine(output.ToString());
                        _deviceLog.WriteLine("--- End of device logs ---");
                    }
                }

                if (errors.Length > 0)
                {
                    _mainLog.WriteLine($"Errors while reading device logs: {errors}");
                }

                logShowProcess.Dispose();
            }
            catch (Exception ex)
            {
                _mainLog.WriteLine($"Failed to read device logs from {_outputPath}: {ex.Message}");
            }
            finally
            {
                try
                {
                    Directory.Delete(_outputPath, true);
                }
                catch (Exception ex)
                {
                    _mainLog.WriteLine($"Failed to delete temporary log archive {_outputPath}: {ex.Message}");
                }
            }
        }

        _process?.Dispose();
    }

    public void Dispose() => StopCapture();
}

