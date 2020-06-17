﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution.Mlaunch;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests.Hardware
{
    public class SimulatorsTest
    {
        private readonly Mock<ILog> _executionLog;
        private readonly Mock<IMLaunchProcessManager> _processManager;
        private readonly SimulatorLoader _simulators;

        public SimulatorsTest()
        {
            _executionLog = new Mock<ILog>();
            _processManager = new Mock<IMLaunchProcessManager>();
            _simulators = new SimulatorLoader(_processManager.Object);
        }

        [Fact]
        public async Task LoadAsyncProcessErrorTest()
        {
            MlaunchArguments passedArguments = null;

            // moq It.Is is not working as nicelly as we would like it, we capture data and use asserts
            _processManager
                .Setup(p => p.ExecuteCommandAsync(It.IsAny<MlaunchArguments>(), It.IsAny<ILog>(), It.IsAny<TimeSpan>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken?>()))
                .Returns<MlaunchArguments, ILog, TimeSpan, Dictionary<string, string>, CancellationToken?>((args, log, t, env, token) =>
                {
                    // we are going set the used args to validate them later, will always return an error from this method
                    passedArguments = args;
                    return Task.FromResult(new ProcessExecutionResult
                    {
                        ExitCode = 1,
                        TimedOut = false
                    });
                });

            await Assert.ThrowsAsync<Exception>(async () =>
            {
                await _simulators.LoadDevices(_executionLog.Object);
            });

            // validate the execution of mlaunch
            MlaunchArgument listSimArg = passedArguments.Where(a => a is ListSimulatorsArgument).FirstOrDefault();
            Assert.NotNull(listSimArg);

            MlaunchArgument outputFormatArg = passedArguments.Where(a => a is XmlOutputFormatArgument).FirstOrDefault();
            Assert.NotNull(outputFormatArg);
        }

        private void CopySampleData(string tempPath)
        {
            string name = GetType().Assembly.GetManifestResourceNames().Where(a => a.EndsWith("simulators.xml", StringComparison.Ordinal)).FirstOrDefault();
            using (var outputStream = new StreamWriter(tempPath))
            using (var sampleStream = new StreamReader(GetType().Assembly.GetManifestResourceStream(name)))
            {
                string line;
                while ((line = sampleStream.ReadLine()) != null)
                {
                    line = line.Replace("{{MAX-IOS.VERSION}}", SdkVersions.MaxiOSDeploymentTarget);
                    line = line.Replace("{{MAX-IOS-VERSION}}", SdkVersions.MaxiOSDeploymentTarget.Replace(".", "-"));

                    line = line.Replace("{{MAX-WATCH.VERSION}}", SdkVersions.MaxWatchDeploymentTarget);
                    line = line.Replace("{{MAX-WATCH-VERSION}}", SdkVersions.MaxWatchDeploymentTarget.Replace(".", "-"));

                    line = line.Replace("{{MAX-TVOS.VERSION}}", SdkVersions.MaxTVOSDeploymentTarget);
                    line = line.Replace("{{MAX-TVOS-VERSION}}", SdkVersions.MaxTVOSDeploymentTarget.Replace(".", "-"));

                    outputStream.WriteLine(line);
                }
            }
        }

        [Fact]
        public async Task LoadAsyncProcessSuccess()
        {
            MlaunchArguments passedArguments = null;

            // moq It.Is is not working as nicelly as we would like it, we capture data and use asserts
            _processManager.Setup(p => p.ExecuteCommandAsync(It.IsAny<MlaunchArguments>(), It.IsAny<ILog>(), It.IsAny<TimeSpan>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken?>()))
                .Returns<MlaunchArguments, ILog, TimeSpan, Dictionary<string, string>, CancellationToken?>((args, log, t, env, token) =>
                {
                    passedArguments = args;

                    // we get the temp file that was passed as the args, and write our sample xml, which will be parsed to get the devices :)
                    string tempPath = args.Where(a => a is ListSimulatorsArgument).First().AsCommandLineArgument();
                    tempPath = tempPath.Substring(tempPath.IndexOf('=') + 1).Replace("\"", string.Empty);

                    CopySampleData(tempPath);
                    return Task.FromResult(new ProcessExecutionResult { ExitCode = 0, TimedOut = false });
                });

            await _simulators.LoadDevices(_executionLog.Object);

            MlaunchArgument listSimArg = passedArguments.Where(a => a is ListSimulatorsArgument).FirstOrDefault();
            Assert.NotNull(listSimArg);

            MlaunchArgument outputFormatArg = passedArguments.Where(a => a is XmlOutputFormatArgument).FirstOrDefault();
            Assert.NotNull(outputFormatArg);

            Assert.Equal(75, _simulators.AvailableDevices.Count());
        }

        [Theory]
        [InlineData(TestTarget.Simulator_iOS64, false)]
        [InlineData(TestTarget.Simulator_iOS32, false)]
        [InlineData(TestTarget.Simulator_tvOS, false)]
        [InlineData(TestTarget.Simulator_watchOS, true)]
        public async Task FindAsyncDoNotCreateTest(TestTarget target, bool shouldFindCompanion)
        {
            MlaunchArguments passedArguments = null;

            _processManager
                .Setup(h => h.ExecuteXcodeCommandAsync("simctl", It.Is<string[]>(args => args[0] == "create"), _executionLog.Object, TimeSpan.FromMinutes(1)))
                .ReturnsAsync(new ProcessExecutionResult() { ExitCode = 0 });

            // moq It.Is is not working as nicelly as we would like it, we capture data and use asserts
            _processManager
                .Setup(p => p.ExecuteCommandAsync(It.IsAny<MlaunchArguments>(), It.IsAny<ILog>(), It.IsAny<TimeSpan>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken?>()))
                .Returns<MlaunchArguments, ILog, TimeSpan, Dictionary<string, string>, CancellationToken?>((args, log, t, env, token) =>
                {
                    passedArguments = args;

                    // we get the temp file that was passed as the args, and write our sample xml, which will be parsed to get the devices :)
                    string tempPath = args.Where(a => a is ListSimulatorsArgument).First().AsCommandLineArgument();
                    tempPath = tempPath.Substring(tempPath.IndexOf('=') + 1).Replace("\"", string.Empty);

                    CopySampleData(tempPath);
                    return Task.FromResult(new ProcessExecutionResult { ExitCode = 0, TimedOut = false });
                });

            await _simulators.LoadDevices(_executionLog.Object);
            (ISimulatorDevice simulator, ISimulatorDevice companion) = await _simulators.FindSimulators(target, _executionLog.Object, false, false);

            Assert.NotNull(simulator);

            if (shouldFindCompanion)
            {
                Assert.NotNull(companion);
            }
            else
            {
                Assert.Null(companion);
            }
        }

        [Fact]
        public async Task FindAsyncExactVersionNotFound()
        {
            MlaunchArguments passedArguments = null;

            _processManager
                .Setup(h => h.ExecuteXcodeCommandAsync("simctl", It.Is<string[]>(args => args[0] == "create"), _executionLog.Object, TimeSpan.FromMinutes(1)))
                .ReturnsAsync(new ProcessExecutionResult() { ExitCode = 0 });

            // moq It.Is is not working as nicelly as we would like it, we capture data and use asserts
            _processManager
                .Setup(p => p.ExecuteCommandAsync(It.IsAny<MlaunchArguments>(), It.IsAny<ILog>(), It.IsAny<TimeSpan>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken?>()))
                .Returns<MlaunchArguments, ILog, TimeSpan, Dictionary<string, string>, CancellationToken?>((args, log, t, env, token) =>
                {
                    passedArguments = args;

                    // we get the temp file that was passed as the args, and write our sample xml, which will be parsed to get the devices :)
                    string tempPath = args.Where(a => a is ListSimulatorsArgument).First().AsCommandLineArgument();
                    tempPath = tempPath.Substring(tempPath.IndexOf('=') + 1).Replace("\"", string.Empty);

                    CopySampleData(tempPath);
                    return Task.FromResult(new ProcessExecutionResult { ExitCode = 0, TimedOut = false });
                });

            await _simulators.LoadDevices(_executionLog.Object);

            await Assert.ThrowsAsync<NoDeviceFoundException>(async () => await _simulators.FindSimulators(new TestTargetOs(TestTarget.Simulator_iOS64, "12.8"), _executionLog.Object, false, false));
        }

        [Fact]
        public async Task FindAsyncExactVersionFound()
        {
            MlaunchArguments passedArguments = null;

            _processManager
                .Setup(h => h.ExecuteXcodeCommandAsync("simctl", It.Is<string[]>(args => args[0] == "create"), _executionLog.Object, TimeSpan.FromMinutes(1)))
                .ReturnsAsync(new ProcessExecutionResult() { ExitCode = 0 });

            // moq It.Is is not working as nicelly as we would like it, we capture data and use asserts
            _processManager
                .Setup(p => p.ExecuteCommandAsync(It.IsAny<MlaunchArguments>(), It.IsAny<ILog>(), It.IsAny<TimeSpan>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken?>()))
                .Returns<MlaunchArguments, ILog, TimeSpan, Dictionary<string, string>, CancellationToken?>((args, log, t, env, token) =>
                {
                    passedArguments = args;

                    // we get the temp file that was passed as the args, and write our sample xml, which will be parsed to get the devices :)
                    string tempPath = args.Where(a => a is ListSimulatorsArgument).First().AsCommandLineArgument();
                    tempPath = tempPath.Substring(tempPath.IndexOf('=') + 1).Replace("\"", string.Empty);

                    CopySampleData(tempPath);
                    return Task.FromResult(new ProcessExecutionResult { ExitCode = 0, TimedOut = false });
                });

            await _simulators.LoadDevices(_executionLog.Object);

            (ISimulatorDevice simulator, ISimulatorDevice _) = await _simulators.FindSimulators(new TestTargetOs(TestTarget.Simulator_iOS64, SdkVersions.MaxiOSSimulator), _executionLog.Object, false, false);
            Assert.NotNull(simulator);
        }

        // This tests the SimulatorEnumerable
        [Theory]
        [InlineData(TestTarget.Simulator_iOS32)]
        [InlineData(TestTarget.Simulator_iOS64)]
        [InlineData(TestTarget.Simulator_tvOS)]
        public void SelectDevicesDeviceOnlyTest(TestTarget testTarget)
        {
            // moq It.Is is not working as nicelly as we would like it, we capture data and use asserts
            _processManager
                .Setup(p => p.ExecuteCommandAsync(It.IsAny<MlaunchArguments>(), It.IsAny<ILog>(), It.IsAny<TimeSpan>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken?>()))
                .Returns<MlaunchArguments, ILog, TimeSpan, Dictionary<string, string>, CancellationToken?>((args, log, t, env, token) =>
                {
                    // We get the temp file that was passed as the args, and write our sample xml, which will be parsed to get the devices :)
                    string tempPath = args.Where(a => a is ListSimulatorsArgument).First().AsCommandLineArgument();
                    tempPath = tempPath.Substring(tempPath.IndexOf('=') + 1).Replace("\"", string.Empty);

                    CopySampleData(tempPath);
                    return Task.FromResult(new ProcessExecutionResult { ExitCode = 0, TimedOut = false });
                });

            var devices = _simulators.SelectDevices(testTarget, _executionLog.Object, false).ToList();

            Assert.Single(devices);
        }

        // This tests the SimulatorEnumerable
        [Fact]
        public void SelectDevicesDeviceAndCompanionTest()
        {
            // moq It.Is is not working as nicelly as we would like it, we capture data and use asserts
            _processManager
                .Setup(p => p.ExecuteCommandAsync(It.IsAny<MlaunchArguments>(), It.IsAny<ILog>(), It.IsAny<TimeSpan>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken?>()))
                .Returns<MlaunchArguments, ILog, TimeSpan, Dictionary<string, string>, CancellationToken?>((args, log, t, env, token) =>
                {
                    // We get the temp file that was passed as the args, and write our sample xml, which will be parsed to get the devices :)
                    string tempPath = args.Where(a => a is ListSimulatorsArgument).First().AsCommandLineArgument();
                    tempPath = tempPath.Substring(tempPath.IndexOf('=') + 1).Replace("\"", string.Empty);

                    CopySampleData(tempPath);
                    return Task.FromResult(new ProcessExecutionResult { ExitCode = 0, TimedOut = false });
                });

            var devices = _simulators.SelectDevices(TestTarget.Simulator_watchOS, _executionLog.Object, false).ToList();

            Assert.Equal(2, devices.Count);
            Assert.True(devices.First().IsWatchSimulator);
            Assert.False(devices.Last().IsWatchSimulator);
        }

        // This tests issues with mlaunch https://github.com/dotnet/xharness/issues/196
        // Mlaunch sometimes times out/returns non-zero exit code and still outputs correct XML
        [Theory]
        [InlineData(0, true)]
        [InlineData(137, false)]
        [InlineData(1, true)]
        public async Task FindSimulatorsWithSucceedingMlaunchTest(int mlaunchExitCode, bool mlaunchTimedout)
        {
            _processManager
                .Setup(h => h.ExecuteXcodeCommandAsync("simctl", It.Is<string[]>(args => args[0] == "create"), _executionLog.Object, TimeSpan.FromMinutes(1)))
                .ReturnsAsync(new ProcessExecutionResult() { ExitCode = 0 });

            // moq It.Is is not working as nicelly as we would like it, we capture data and use asserts
            _processManager
                .Setup(p => p.ExecuteCommandAsync(It.IsAny<MlaunchArguments>(), It.IsAny<ILog>(), It.IsAny<TimeSpan>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken?>()))
                .Returns<MlaunchArguments, ILog, TimeSpan, Dictionary<string, string>, CancellationToken?>((args, log, t, env, token) =>
                {
                    // we get the temp file that was passed as the args, and write our sample xml, which will be parsed to get the devices :)
                    string tempPath = args.Where(a => a is ListSimulatorsArgument).First().AsCommandLineArgument();
                    tempPath = tempPath.Substring(tempPath.IndexOf('=') + 1).Replace("\"", string.Empty);

                    CopySampleData(tempPath);
                    return Task.FromResult(new ProcessExecutionResult { ExitCode = mlaunchExitCode, TimedOut = mlaunchTimedout });
                });

            await _simulators.LoadDevices(_executionLog.Object);
            (ISimulatorDevice simulator, ISimulatorDevice companion) = await _simulators.FindSimulators(TestTarget.Simulator_iOS64, _executionLog.Object, false, false);

            Assert.NotNull(simulator);
        }

        // This tests issues with mlaunch https://github.com/dotnet/xharness/issues/196
        // Mlaunch sometimes times out/returns non-zero exit code and doesn't output simulator list XML
        [Fact]
        public async Task FindSimulatorsWithFailingMlaunchTest()
        {
            _processManager
                .Setup(h => h.ExecuteXcodeCommandAsync("simctl", It.Is<string[]>(args => args[0] == "create"), _executionLog.Object, TimeSpan.FromMinutes(1)))
                .ReturnsAsync(new ProcessExecutionResult() { ExitCode = 0 });

            // moq It.Is is not working as nicelly as we would like it, we capture data and use asserts
            _processManager
                .Setup(p => p.ExecuteCommandAsync(It.IsAny<MlaunchArguments>(), It.IsAny<ILog>(), It.IsAny<TimeSpan>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken?>()))
                .ReturnsAsync(new ProcessExecutionResult { ExitCode = 137, TimedOut = true });

            await Assert.ThrowsAsync<Exception>(async () => await _simulators.LoadDevices(_executionLog.Object));
        }
    }
}
