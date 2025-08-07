// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
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
    private readonly string _outputPath;

    public DeviceLogCapturer(IMlaunchProcessManager processManager, ILog mainLog, ILog deviceLog, string deviceUdid)
    {
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        _mainLog = mainLog ?? throw new ArgumentNullException(nameof(mainLog));
        _deviceLog = deviceLog ?? throw new ArgumentNullException(nameof(deviceLog));
        _deviceUdid = deviceUdid ?? throw new ArgumentNullException(nameof(deviceUdid));
        _outputPath = Path.GetTempFileName();
    }

    private Process _process;

    public void StartCapture()
    {
        // Use sudo log collect instead of mlaunch --logdev
        var startTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var arguments = $"log collect --device-udid {_deviceUdid} --start \"{startTime}\" --output \"{_outputPath}\"";

        _process = new Process();
        _process.StartInfo.FileName = "sudo";
        _process.StartInfo.Arguments = arguments;
        _process.StartInfo.UseShellExecute = false;
        _process.StartInfo.RedirectStandardOutput = true;
        _process.StartInfo.RedirectStandardError = true;
        _process.StartInfo.RedirectStandardInput = true;

        _process.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
        {
            if (e.Data == null)
            {
                return;
            }

            lock (_deviceLog)
            {
                _deviceLog.WriteLine(e.Data);
            }
        };

        _process.ErrorDataReceived += (object sender, DataReceivedEventArgs e) =>
        {
            if (e.Data == null)
            {
                return;
            }

            lock (_deviceLog)
            {
                _deviceLog.WriteLine(e.Data);
            }
        };

        _deviceLog.WriteLine("{0} {1}", _process.StartInfo.FileName, _process.StartInfo.Arguments);

        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    public void StopCapture()
    {
        if (_process?.HasExited == false)
        {
            try
            {
                _process.Kill();
                _process.WaitForExit((int)TimeSpan.FromSeconds(5).TotalMilliseconds);
            }
            catch (InvalidOperationException)
            {
                // Process already exited
            }
        }

        // Read the collected logs from the output file
        if (File.Exists(_outputPath))
        {
            try
            {
                var logContent = File.ReadAllText(_outputPath);
                if (!string.IsNullOrEmpty(logContent))
                {
                    lock (_deviceLog)
                    {
                        _deviceLog.WriteLine("--- Device logs collected ---");
                        _deviceLog.WriteLine(logContent);
                        _deviceLog.WriteLine("--- End of device logs ---");
                    }
                }
            }
            catch (Exception ex)
            {
                _mainLog.WriteLine($"Failed to read device logs from {_outputPath}: {ex.Message}");
            }
            finally
            {
                try
                {
                    File.Delete(_outputPath);
                }
                catch (Exception ex)
                {
                    _mainLog.WriteLine($"Failed to delete temporary log file {_outputPath}: {ex.Message}");
                }
            }
        }

        _process?.Dispose();
    }

    public void Dispose() => StopCapture();
}

