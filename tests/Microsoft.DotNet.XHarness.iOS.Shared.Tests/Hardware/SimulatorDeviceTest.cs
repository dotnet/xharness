// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests.Hardware
{
    public class SimulatorDeviceTest : IDisposable
    {
        private Mock<ILog> executionLog;
        private Mock<IProcessManager> processManager;
        private SimulatorDevice simulator;

        public SimulatorDeviceTest()
        {
            executionLog = new Mock<ILog>();
            processManager = new Mock<IProcessManager>();
            simulator = new SimulatorDevice(processManager.Object, new TCCDatabase(processManager.Object))
            {
                UDID = Guid.NewGuid().ToString()
            };
        }

        public void Dispose()
        {
            executionLog = null;
            processManager = null;
            simulator = null;
        }

        [Theory]
        [InlineData("com.apple.CoreSimulator.SimRuntime.watchOS-5-1", true)]
        [InlineData("com.apple.CoreSimulator.SimRuntime.iOS-7-1", false)]
        public void IsWatchSimulatorTest(string runtime, bool expectation)
        {
            simulator.SimRuntime = runtime;
            Assert.Equal(expectation, simulator.IsWatchSimulator);
        }

        [Theory]
        [InlineData("com.apple.CoreSimulator.SimRuntime.iOS-12-1", "iOS 12.1")]
        [InlineData("com.apple.CoreSimulator.SimRuntime.iOS-10-1", "iOS 10.1")]
        public void OSVersionTest(string runtime, string expected)
        {
            simulator.SimRuntime = runtime;
            Assert.Equal(expected, simulator.OSVersion);
        }

        [Fact]
        public async Task EraseAsyncTest()
        {
            // just call and verify the correct args are pass
            await simulator.Erase(executionLog.Object);
            processManager.Verify(h => h.ExecuteXcodeCommandAsync(It.Is<string>(s => s == "simctl"), It.Is<string[]>(args => args.Where(a => a == simulator.UDID || a == "shutdown").Count() == 2), It.IsAny<ILog>(), It.IsAny<TimeSpan>()));
            processManager.Verify(h => h.ExecuteXcodeCommandAsync(It.Is<string>(s => s == "simctl"), It.Is<string[]>(args => args.Where(a => a == simulator.UDID || a == "erase").Count() == 2), It.IsAny<ILog>(), It.IsAny<TimeSpan>()));
            processManager.Verify(h => h.ExecuteXcodeCommandAsync(It.Is<string>(s => s == "simctl"), It.Is<string[]>(args => args.Where(a => a == simulator.UDID || a == "boot").Count() == 2), It.IsAny<ILog>(), It.IsAny<TimeSpan>()));
            processManager.Verify(h => h.ExecuteXcodeCommandAsync(It.Is<string>(s => s == "simctl"), It.Is<string[]>(args => args.Where(a => a == simulator.UDID || a == "shutdown").Count() == 2), It.IsAny<ILog>(), It.IsAny<TimeSpan>()));

        }

        [Fact]
        public async Task ShutdownAsyncTest()
        {
            await simulator.Shutdown(executionLog.Object);
            // just call and verify the correct args are pass
            processManager.Verify(h => h.ExecuteXcodeCommandAsync(It.Is<string>(s => s == "simctl"), It.Is<string[]>(args => args.Where(a => a == simulator.UDID || a == "shutdown").Count() == 2), It.IsAny<ILog>(), It.IsAny<TimeSpan>()));
        }

        [Fact(Skip = "Running this test will actually kill simulators on the machine")]
        public async Task KillEverythingAsyncTest()
        {
            Func<IList<string>, bool> verifyKillAll = (args) =>
            {
                var toKill = new List<string> { "-9", "iPhone Simulator", "iOS Simulator", "Simulator", "Simulator (Watch)", "com.apple.CoreSimulator.CoreSimulatorService", "ibtoold" };
                return args.Where(a => toKill.Contains(a)).Count() == toKill.Count;
            };

            var simulator = new SimulatorDevice(processManager.Object, new TCCDatabase(processManager.Object));
            await simulator.KillEverything(executionLog.Object);

            // verify that all the diff process have been killed making sure args are correct
            processManager.Verify(p => p.ExecuteCommandAsync(It.Is<string>(s => s == "launchctl"), It.Is<string[]>(args => args.Where(a => a == "remove" || a == "com.apple.CoreSimulator.CoreSimulatorService").Count() == 2), It.IsAny<ILog>(), It.IsAny<TimeSpan>(), null, null));
            processManager.Verify(p => p.ExecuteCommandAsync(It.Is<string>(s => s == "killall"), It.Is<IList<string>>(a => verifyKillAll(a)), It.IsAny<ILog>(), It.IsAny<TimeSpan>(), null, null));
        }

    }
}
