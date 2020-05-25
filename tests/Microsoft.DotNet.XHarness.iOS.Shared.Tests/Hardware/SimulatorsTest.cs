// Licensed to the .NET Foundation under one or more agreements.
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
            var name = GetType().Assembly.GetManifestResourceNames().Where(a => a.EndsWith("simulators.xml", StringComparison.Ordinal)).FirstOrDefault();
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
                    var tempPath = args.Where(a => a is ListSimulatorsArgument).First().AsCommandLineArgument();
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
                    var tempPath = args.Where(a => a is ListSimulatorsArgument).First().AsCommandLineArgument();
                    tempPath = tempPath.Substring(tempPath.IndexOf('=') + 1).Replace("\"", string.Empty);

                    CopySampleData(tempPath);
                    return Task.FromResult(new ProcessExecutionResult { ExitCode = 0, TimedOut = false });
                });

            await _simulators.LoadDevices(_executionLog.Object);
            var (simulator, companion) = await _simulators.FindSimulators(target, _executionLog.Object, false, false);

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
    }
}
