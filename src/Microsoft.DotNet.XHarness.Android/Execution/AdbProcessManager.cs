using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.Android.Execution
{
    public class AdbProcessManager : IAdbProcessManager
    {
        private readonly ILogger _log;
        public AdbProcessManager(ILogger logger) => _log = logger;

        /// <summary>
        ///  Whenever there are multiple devices attached to a system, most ADB commands will fail
        ///  unless the specific device id is provided with -s {device serial #}
        /// </summary>
        public string DeviceSerial { get; set; } = string.Empty;

        public ProcessExecutionResults Run(string adbExePath, string arguments) => Run(adbExePath, arguments, TimeSpan.FromMinutes(5));

        public ProcessExecutionResults Run(string adbExePath, string arguments, TimeSpan timeOut)
        {
            string deviceSerialArgs = string.IsNullOrEmpty(DeviceSerial) ? string.Empty : $"-s {DeviceSerial}";

            _log.LogDebug($"Executing command: '{adbExePath} {deviceSerialArgs} {arguments}'");

            var processStartInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(adbExePath),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                FileName = adbExePath,
                Arguments = $"{deviceSerialArgs} {arguments}",
            };
            var p = new Process() { StartInfo = processStartInfo };
            var standardOut = new StringBuilder();
            var standardErr = new StringBuilder();

            p.OutputDataReceived += delegate (object sender, DataReceivedEventArgs e)
            {
                lock (standardOut)
                {
                    if (e.Data != null)
                    {
                        standardOut.AppendLine(e.Data);
                    }
                }
            };

            p.ErrorDataReceived += delegate (object sender, DataReceivedEventArgs e)
            {
                lock (standardErr)
                {
                    if (e.Data != null)
                    {
                        standardErr.AppendLine(e.Data);
                    }
                }
            };

            p.Start();

            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            bool timedOut = false;
            int exitCode;

            // (int.MaxValue ms is about 24 days).  Large values are effectively timeouts for the outer harness
            if (!p.WaitForExit((int)Math.Min(timeOut.TotalMilliseconds, int.MaxValue)))
            {
                _log.LogError("Waiting for command timed out: execution may be compromised.");
                timedOut = true;
                exitCode = (int)AdbExitCodes.INSTRUMENTATION_TIMEOUT;

                // try to terminate the process
                try { p.Kill (); } catch { }
            }
            else
            {
                // we exited normally, call WaitForExit() again to ensure redirected standard output is processed
                p.WaitForExit();
                exitCode = p.ExitCode;
            }

            p.Close();

            lock (standardOut)
                lock (standardErr)
                {
                    return new ProcessExecutionResults()
                    {
                        ExitCode = exitCode,
                        StandardOutput = standardOut.ToString(),
                        StandardError = standardErr.ToString(),
                        TimedOut = timedOut
                    };
                }
        }
    }
}
