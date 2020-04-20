// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Listeners;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests
{
    public class TestReporterTests : IDisposable
    {
        private readonly Mock<ICrashSnapshotReporter> crashReporter;
        private Mock<IProcessManager> processManager;
        private readonly IResultParser parser;
        private Mock<ILog> runLog;
        private Mock<ILog> mainLog;
        private readonly Mock<ILogs> logs;
        private Mock<ISimpleListener> listener;
        private readonly AppBundleInformation appInformation;
        private readonly string deviceName = "Device Name";
        private readonly string logsDirectory;

        public TestReporterTests()
        {
            crashReporter = new Mock<ICrashSnapshotReporter>();
            processManager = new Mock<IProcessManager>();
            parser = new XmlResultParser();
            runLog = new Mock<ILog>();
            mainLog = new Mock<ILog>();
            logs = new Mock<ILogs>();
            listener = new Mock<ISimpleListener>();
            appInformation = new AppBundleInformation("test app", "my.id.com", "/path/to/app", "/launch/app/path", null) { Variation = "Debug" };
            logsDirectory = Path.GetTempFileName();
            File.Delete(logsDirectory);
            Directory.CreateDirectory(logsDirectory);
        }

        public void Dispose()
        {
            processManager = null;
            runLog = null;
            mainLog = null;
            listener = null;
            if (Directory.Exists(logsDirectory))
                Directory.Delete(logsDirectory, true);
        }

        private Stream GetRunLogSample()
        {
            var name = GetType().Assembly.GetManifestResourceNames().Where(a => a.EndsWith("run-log.txt", StringComparison.Ordinal)).FirstOrDefault();
            return GetType().Assembly.GetManifestResourceStream(name);
        }

        private TestReporter BuildTestResult()
        {
            logs.Setup(l => l.Directory).Returns(logsDirectory);

            return new TestReporter(processManager.Object,
                mainLog.Object,
                runLog.Object,
                logs.Object,
                crashReporter.Object,
                listener.Object,
                parser,
                appInformation,
                RunMode.Sim64,
                XmlResultJargon.NUnitV3,
                deviceName,
                TimeSpan.FromSeconds(2));
        }

        [Fact]
        public async Task CollectSimulatorResultsSucess()
        {
            // set the listener to return a task that we are not going to complete
            var cancellationTokenSource = new CancellationTokenSource();
            var tcs = new TaskCompletionSource<object>();
            listener.Setup(l => l.CompletionTask).Returns(tcs.Task); // will never be set to be completed

            // ensure that we do provide the required runlog information so that we know if it was a launch failure or not, we are
            // not dealing with the launch faliure
            runLog.Setup(l => l.GetReader()).Returns(new StreamReader(GetRunLogSample()));

            var testResult = BuildTestResult();
            var processResult = Task.FromResult(new ProcessExecutionResult() { TimedOut = false, ExitCode = 0 });
            await testResult.CollectSimulatorResult(processResult);
            // we should have timeout, since the task completion source was never set
            Assert.True(testResult.Success, "success");

            processManager.Verify(p => p.KillTreeAsync(It.IsAny<int>(), It.IsAny<ILog>(), true), Times.Never);
        }

        // we need to make sure that we take into account the case in which we do have data, but no PID and an empty file
        // which is a catastrophic launch error
        [Theory]
        [InlineData("Some Data")]
        [InlineData(null)]
        public async Task CollectSimulatorResultsLaunchFailureTest(string runLogData)
        {
            // similar to the above test, but in this case we ware going to fake a launch issue, that is, the runlog
            // does not contain a PID that we can parse and later try to kill.

            // set the listener to return a task that we are not going to complete
            var cancellationTokenSource = new CancellationTokenSource();
            var tcs = new TaskCompletionSource<object>();
            listener.Setup(l => l.CompletionTask).Returns(tcs.Task); // will never be set to be completed

            // empty test file to be returned as the runlog stream
            var tmpFile = Path.GetTempFileName();
            if (!string.IsNullOrEmpty(runLogData))
            {
                using (var writer = new StreamWriter(tmpFile))
                {
                    writer.Write(runLogData);
                }
            }

            // ensure that we do provide the required runlog information so that we know if it was a launch failure or not, we are
            // not dealing with the launch faliure
            runLog.Setup(l => l.GetReader()).Returns(new StreamReader(File.Create(tmpFile)));

            var testResult = BuildTestResult();
            var processResult = Task.FromResult(new ProcessExecutionResult() { TimedOut = true, ExitCode = 0 });
            await testResult.CollectSimulatorResult(processResult);
            // we should have timeout, since the task completion source was never set
            Assert.False(testResult.Success, "success");

            // verify that we do not try to kill a process that never got started
            processManager.Verify(p => p.KillTreeAsync(It.IsAny<int>(), It.IsAny<ILog>(), true), Times.Never);
            File.Delete(tmpFile);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task CollectSimulatorResultsSuccessLaunchTest(int processExitCode)
        {
            // fake the best case scenario, we got the process to exit correctly
            var cancellationTokenSource = new CancellationTokenSource();
            var tcs = new TaskCompletionSource<object>();
            var processResult = Task.FromResult(new ProcessExecutionResult() { TimedOut = false, ExitCode = processExitCode });

            // ensure we do not consider it to be a launch failure
            runLog.Setup(l => l.GetReader()).Returns(new StreamReader(GetRunLogSample()));

            var testResult = BuildTestResult();
            await testResult.CollectSimulatorResult(processResult);

            // we should have timeout, since the task completion source was never set
            if (processExitCode != 0)
                Assert.False(testResult.Success, "success");
            else
                Assert.True(testResult.Success, "success");

            if (processExitCode != 0)
                processManager.Verify(p => p.KillTreeAsync(It.IsAny<int>(), It.IsAny<ILog>(), true), Times.Once);
            else
                // verify that we do not try to kill a process that never got started
                processManager.Verify(p => p.KillTreeAsync(It.IsAny<int>(), It.IsAny<ILog>(), true), Times.Never);
        }

        [Fact]
        public async Task CollectDeviceResultTimeoutTest()
        {
            // set the listener to return a task that we are not going to complete
            var tcs = new TaskCompletionSource<object>();
            listener.Setup(l => l.CompletionTask).Returns(tcs.Task); // will never be set to be completed

            // ensure that we do provide the required runlog information so that we know if it was a launch failure or not, we are
            // not dealing with the launch faliure
            runLog.Setup(l => l.GetReader()).Returns(new StreamReader(GetRunLogSample()));

            var testResult = BuildTestResult();
            var processResult = Task.FromResult(new ProcessExecutionResult() { TimedOut = true, ExitCode = 0 });
            await testResult.CollectDeviceResult(processResult);
            // we should have timeout, since the task completion source was never set
            Assert.False(testResult.Success, "success");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task CollectDeviceResultSuccessTest(int processExitCode)
        {
            // fake the best case scenario, we got the process to exit correctly
            var processResult = Task.FromResult(new ProcessExecutionResult() { TimedOut = false, ExitCode = processExitCode });

            // ensure we do not consider it to be a launch failure
            runLog.Setup(l => l.GetReader()).Returns(new StreamReader(GetRunLogSample()));

            var testResult = BuildTestResult();
            await testResult.CollectDeviceResult(processResult);

            // we should have timeout, since the task completion source was never set
            if (processExitCode != 0)
                Assert.False(testResult.Success, "success");
            else
                Assert.True(testResult.Success, "success");
        }

        [Fact]
        public void LaunchCallbackFaultedTest()
        {
            var testResult = BuildTestResult();
            var t = Task.FromException<bool>(new Exception("test"));
            testResult.LaunchCallback(t);
            // verify that we did report the launch proble
            mainLog.Verify(l => l.WriteLine(
               It.Is<string>(s => s.StartsWith($"Test launch failed:")), It.IsAny<object>()), Times.Once);
        }

        [Fact]
        public void LaunchCallbackCanceledTest()
        {
            var testResult = BuildTestResult();
            var tcs = new TaskCompletionSource<bool>();
            tcs.TrySetCanceled();
            testResult.LaunchCallback(tcs.Task);
            // verify we notify that the execution was canceled
            mainLog.Verify(l => l.WriteLine(It.Is<string>(s => s.Equals("Test launch was cancelled."))), Times.Once);
        }

        [Fact]
        public void LaunchCallbackSuccessTest()
        {
            var testResult = BuildTestResult();
            var t = Task.FromResult(true);
            testResult.LaunchCallback(t);
            mainLog.Verify(l => l.WriteLine(It.Is<string>(s => s.Equals("Test run started"))), Times.Once);
        }

        // copy the sample data to a given tmp file
        private string CreateSampleFile(string resourceName)
        {
            var name = GetType().Assembly.GetManifestResourceNames().Where(a => a.EndsWith(resourceName, StringComparison.Ordinal)).FirstOrDefault();
            var tempPath = Path.GetTempFileName();
            using var outputStream = new StreamWriter(tempPath);
            using var sampleStream = new StreamReader(GetType().Assembly.GetManifestResourceStream(name));
            string line;
            while ((line = sampleStream.ReadLine()) != null)
                outputStream.WriteLine(line);
            return tempPath;
        }

        [Fact]
        public async Task ParseResultFailingTestsTest()
        {
            var sample = CreateSampleFile("NUnitV3SampleFailure.xml");
            var listenerLog = new Mock<ILog>();
            listener.Setup(l => l.TestLog).Returns(listenerLog.Object);
            listenerLog.Setup(l => l.FullPath).Returns(sample);

            var testResult = BuildTestResult();
            var (result, failure) = await testResult.ParseResult();
            Assert.Equal(TestExecutingResult.Failed, result);
            Assert.Null(failure);

            // ensure that we do  call the crash reporter end capture but with 0, since it was a success
            crashReporter.Verify(c => c.EndCaptureAsync(It.Is<TimeSpan>(t => t.TotalSeconds == 5)), Times.Once);
        }

        [Fact]
        public async Task ParseResultSuccessTestsTest()
        {
            // get a file with a success result so that we can return it as part of the listener log
            var sample = CreateSampleFile("NUnitV3SampleSuccess.xml");
            var listenerLog = new Mock<ILog>();
            listener.Setup(l => l.TestLog).Returns(listenerLog.Object);
            listenerLog.Setup(l => l.FullPath).Returns(sample);

            var testResult = BuildTestResult();
            var (result, failure) = await testResult.ParseResult();
            Assert.Equal(TestExecutingResult.Succeeded, result);
            Assert.Null(failure);

            // ensure that we do  call the crash reporter end capture but with 0, since it was a success
            crashReporter.Verify(c => c.EndCaptureAsync(It.Is<TimeSpan>(t => t.TotalSeconds == 0)), Times.Once);
        }

        [Fact]
        public async Task ParseResultTimeoutTestsTest()
        {
            // more complicated test, we need to fake a process timeout, then ensure that the result is the expected one
            var tcs = new TaskCompletionSource<object>();
            listener.Setup(l => l.CompletionTask).Returns(tcs.Task); // will never be set to be completed

            var listenerLog = new Mock<ILog>();
            listener.Setup(l => l.TestLog).Returns(listenerLog.Object);
            listenerLog.Setup(l => l.FullPath).Returns("/my/missing/path");

            // ensure that we do provide the required runlog information so that we know if it was a launch failure or not, we are
            // not dealing with the launch faliure
            runLog.Setup(l => l.GetReader()).Returns(new StreamReader(GetRunLogSample()));

            var failurePath = Path.Combine(logsDirectory, "my-failure.xml");
            var failureLog = new Mock<ILog>();
            failureLog.Setup(l => l.FullPath).Returns(failurePath);
            logs.Setup(l => l.Create(It.IsAny<string>(), It.IsAny<string>(), null)).Returns(failureLog.Object);

            // create some data for the stderr
            var stderr = Path.GetTempFileName();
            using (var stream = File.Create(stderr))
            using (var writer = new StreamWriter(stream))
            {
                await writer.WriteAsync("Some data to be added to stderr of the failure");
            }
            mainLog.Setup(l => l.FullPath).Returns(stderr);

            var testResult = BuildTestResult();
            var processResult = Task.FromResult(new ProcessExecutionResult() { TimedOut = true, ExitCode = 0 });
            await testResult.CollectDeviceResult(processResult);
            // we should have timeout, since the task completion source was never set
            var (result, failure) = await testResult.ParseResult();
            Assert.False(testResult.Success, "success");

            // verify that we state that there was a timeout
            mainLog.Verify(l => l.WriteLine(It.Is<string>(s => s.Equals("Test run never launched"))), Times.Once);
            // assert that the timeout failure was created.
            Assert.True(File.Exists(failurePath), "failure path");
            var isTimeoutFailure = false;
            using (var reader = new StreamReader(failurePath))
            {
                string line = null;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (line.Contains("App Timeout"))
                    {
                        isTimeoutFailure = true;
                        break;
                    }
                }
            }
            Assert.True(isTimeoutFailure, "correct xml");
            File.Delete(failurePath);
        }
    }
}
