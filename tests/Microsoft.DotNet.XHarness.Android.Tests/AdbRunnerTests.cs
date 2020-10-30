// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.DotNet.XHarness.Android.Execution;
using Microsoft.Extensions.Logging;

using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.Android.Tests
{
    public class AdbRunnerTests : IDisposable
    {
        private static readonly string s_scratchAndOutputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        private static readonly string s_adbPath = Path.Combine(s_scratchAndOutputPath, "adb");
        private static string s_currentDeviceSerial = "";
        private readonly Mock<ILogger> _mainLog;
        private readonly Mock<IAdbProcessManager> _processManager;
        private readonly Dictionary<Tuple<string, string>, int> _fakeDeviceList;


        public AdbRunnerTests()
        {
            _mainLog = new Mock<ILogger>();

            _processManager = new Mock<IAdbProcessManager>();

            // Fake devices to pretend are attached to the system
            _fakeDeviceList = InitializeFakeDeviceList();

            // Fake ADB executable since its path is checked 
            Directory.CreateDirectory(s_scratchAndOutputPath);
            File.Create(s_adbPath).Close();

            // Mock to check the args ADB actually gets called with
            _processManager.Setup(pm => pm.Run(
               It.IsAny<string>(), // process, not checking the value to match any call
               It.IsAny<string>(), // same
               It.IsAny<TimeSpan>())).Returns((string p, string a, TimeSpan t) => CallFakeProcessManager(p, a, t));
        }

        public void Dispose() => Directory.Delete(s_scratchAndOutputPath, true);

        #region Tests

        [Fact]
        public void GetAdbState()
        {
            var runner = new AdbRunner(_mainLog.Object, _processManager.Object, s_adbPath);
            string result = runner.GetAdbState();
            _processManager.Verify(pm => pm.Run(s_adbPath, "get-state", TimeSpan.FromMinutes(5)), Times.Once);
            Assert.Equal("device", result);
        }

        [Fact]
        public void ClearAdbLog()
        {
            var runner = new AdbRunner(_mainLog.Object, _processManager.Object, s_adbPath);
            runner.ClearAdbLog();
            _processManager.Verify(pm => pm.Run(s_adbPath, "logcat -c", TimeSpan.FromMinutes(5)), Times.Once);
        }
        [Fact]
        public void DumpAdbLog()
        {
            var runner = new AdbRunner(_mainLog.Object, _processManager.Object, s_adbPath);
            string pathToDumpLogTo = Path.Join(s_scratchAndOutputPath, $"{Path.GetRandomFileName()}.log");
            runner.DumpAdbLog(pathToDumpLogTo);
            _processManager.Verify(pm => pm.Run(s_adbPath, "logcat -d ", TimeSpan.FromMinutes(2)), Times.Once);

            Assert.Equal("Sample LogCat Output", File.ReadAllText(pathToDumpLogTo));
        }

        [Fact]
        public void DumpBugReport()
        {
            var runner = new AdbRunner(_mainLog.Object, _processManager.Object, s_adbPath);
            string pathToDumpBugReport = Path.Join(s_scratchAndOutputPath, $"{Path.GetRandomFileName()}.zip");
            runner.DumpBugReport(pathToDumpBugReport);
            _processManager.Verify(pm => pm.Run(s_adbPath, $"bugreport {pathToDumpBugReport}", TimeSpan.FromMinutes(5)), Times.Once);

            Assert.Equal("Sample BugReport Output", File.ReadAllText(pathToDumpBugReport));
        }

        [Fact]
        public void WaitForDevice()
        {
            var runner = new AdbRunner(_mainLog.Object, _processManager.Object, s_adbPath);
            string fakeDeviceName = $"emulator-{new Random().Next(9999)}";
            runner.SetActiveDevice(fakeDeviceName);
            runner.WaitForDevice();
            runner.SetActiveDevice(null);
            runner.WaitForDevice();
            _processManager.Verify(pm => pm.Run(s_adbPath, $"wait-for-device", TimeSpan.FromMinutes(5)), Times.Exactly(2));
        }

        [Fact]
        public void ListDevicesAndArchitectures()
        {
            var runner = new AdbRunner(_mainLog.Object, _processManager.Object, s_adbPath);
            var result = runner.GetAttachedDevicesAndArchitectures();
            _processManager.Verify(pm => pm.Run(s_adbPath, "devices -l", TimeSpan.FromSeconds(30)), Times.Once);

            // Ensure it called, parsed the three random device names and found all three architectures
            foreach (var fakeDeviceInfo in _fakeDeviceList.Keys)
            {
                _processManager.Verify(pm => pm.Run(s_adbPath, $"-s {fakeDeviceInfo.Item1} shell getprop ro.product.cpu.abi", TimeSpan.FromMinutes(5)), Times.Once);
                Assert.Equal(fakeDeviceInfo.Item2, result[fakeDeviceInfo.Item1]);

            }
            Assert.Equal(3, result.Count);
        }

        [Fact]
        public void InstallApk()
        {
            var runner = new AdbRunner(_mainLog.Object, _processManager.Object, s_adbPath);
            string fakeApkPath = Path.Join(s_scratchAndOutputPath, $"{Path.GetRandomFileName()}.apk");
            File.Create(fakeApkPath).Close();
            int exitCode = runner.InstallApk(fakeApkPath);
            _processManager.Verify(pm => pm.Run(s_adbPath, $"install \"{fakeApkPath}\"", TimeSpan.FromMinutes(5)), Times.Once);
            Assert.Equal(0, exitCode);
        }

        [Fact]
        public void UninstallApk()
        {
            var runner = new AdbRunner(_mainLog.Object, _processManager.Object, s_adbPath);
            string fakeApkName = $"{Path.GetRandomFileName()}";
            int exitCode = runner.UninstallApk(fakeApkName);
            _processManager.Verify(pm => pm.Run(s_adbPath, $"uninstall {fakeApkName}", TimeSpan.FromMinutes(5)), Times.Once);
            Assert.Equal(0, exitCode);
        }

        [Fact]
        public void KillApk()
        {
            var runner = new AdbRunner(_mainLog.Object, _processManager.Object, s_adbPath);
            string fakeApkName = $"{Path.GetRandomFileName()}";
            int exitCode = runner.KillApk(fakeApkName);
            _processManager.Verify(pm => pm.Run(s_adbPath, $"shell am kill --user all {fakeApkName}", TimeSpan.FromMinutes(5)), Times.Once);
            Assert.Equal(0, exitCode);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("FakeInstrumentationName")]
        public void RunInstrumentation(string instrumentationName)
        {
            string fakeApkName = Path.GetRandomFileName();
            var runner = new AdbRunner(_mainLog.Object, _processManager.Object, s_adbPath);

            ProcessExecutionResults result;
            var fakeArgs = new Dictionary<string, string>()
            {
                { "arg1", "value1" },
                { "arg2", "value2" }
            };

            result = runner.RunApkInstrumentation(fakeApkName, instrumentationName, fakeArgs, TimeSpan.FromSeconds(123));
            Assert.Equal(0, result.ExitCode);

            result = runner.RunApkInstrumentation(fakeApkName, instrumentationName, new Dictionary<string, string>(), TimeSpan.FromSeconds(456));
            Assert.Equal(0, result.ExitCode);

            if (string.IsNullOrEmpty(instrumentationName))
            {
                _processManager.Verify(pm => pm.Run(s_adbPath, $"shell am instrument  -e arg1 value1 -e arg2 value2 -w {fakeApkName}", TimeSpan.FromSeconds(123)), Times.Once);
                _processManager.Verify(pm => pm.Run(s_adbPath, $"shell am instrument  -w {fakeApkName}", TimeSpan.FromSeconds(456)), Times.Once);
            }
            else
            {
                _processManager.Verify(pm => pm.Run(s_adbPath, $"shell am instrument  -e arg1 value1 -e arg2 value2 -w {fakeApkName}/{instrumentationName}", TimeSpan.FromSeconds(123)), Times.Once);
                _processManager.Verify(pm => pm.Run(s_adbPath, $"shell am instrument  -w {fakeApkName}/{instrumentationName}", TimeSpan.FromSeconds(456)), Times.Once);
            }
        }

        #endregion

        #region Helper Functions
        // Generates a list of fake devices, one per supported architecture so we can test AdbRunner's parsing of the output.
        // As with most of these tests, if adb.exe changes (we are locked into specific version) 
        private Dictionary<Tuple<string, string>, int> InitializeFakeDeviceList()
        {
            var r = new Random();
            var values = new Dictionary<Tuple<string, string>, int>
            {
                { new Tuple<string, string>($"somedevice-{r.Next(9999)}", "x86_64"), 0 },
                { new Tuple<string, string>($"somedevice-{r.Next(9999)}", "x86"), 0 },
                { new Tuple<string, string>($"somedevice-{r.Next(9999)}", "arm64v8"), 0 }
            };
            return values;
        }

        private ProcessExecutionResults CallFakeProcessManager(string process, string arguments, TimeSpan timeout)
        {
            if (Debugger.IsAttached)
            {
                Debug.WriteLine($"Fake ADB Process Manager invoked with args: '{process} {arguments}' (timeout = {timeout.TotalSeconds})");
            }

            bool timedOut = false;
            int exitCode = 0;
            string stdOut = "";
            string stdErr = "";

            string[] allArgs = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            int argStart = 0;

            if (allArgs[0] == "-s")
            {
                s_currentDeviceSerial = allArgs[1];
                argStart = 2;
            }

            switch (allArgs[argStart].ToLowerInvariant())
            {
                case "get-state":
                    stdOut = "device";
                    exitCode = 0;
                    break;
                case "devices":
                    var s = new StringBuilder();
                    int transportId = 1;
                    s.AppendLine("List of devices attached");
                    foreach (var device in _fakeDeviceList)
                    {
                        string offlineMsg = _fakeDeviceList[device.Key]++ > 4 ? "offline" : "online";
                        s.AppendLine($"{device.Key.Item1}          {offlineMsg} transportid:{transportId++}");
                    }
                    stdOut = s.ToString();
                    break;
                case "shell":
                    if ($"{allArgs[argStart + 1]} {allArgs[argStart + 2]}".Equals("getprop ro.product.cpu.abi"))
                    {
                        stdOut = _fakeDeviceList.Keys.Where(k => k.Item1 == s_currentDeviceSerial).Single().Item2;
                    }
                    exitCode = 0;
                    break;
                case "logcat":
                    if (allArgs[argStart + 1].Equals("-c")) { }; // Do nothing
                    if (allArgs[argStart + 1].Equals("-d"))
                    {
                        stdOut = "Sample LogCat Output";
                    }
                    break;
                case "bugreport":
                    var outputPath = allArgs[argStart + 1];
                    File.WriteAllText(outputPath, "Sample BugReport Output");
                    break;
                case "install":
                case "uninstall":
                case "wait-for-device":
                    // No output needed, but pretend to wait a little
                    Thread.Sleep(1000);
                    break;
                default:
                    throw new InvalidOperationException($"Fake ADB doesn't know how to handle argument: {arguments}");
            }

            return new ProcessExecutionResults
            {
                TimedOut = timedOut,
                ExitCode = exitCode,
                StandardError = stdErr,
                StandardOutput = stdOut
            };
        }
    }
    #endregion
}
