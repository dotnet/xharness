using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.iOS;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution.Mlaunch;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;
using Moq;
using Xunit;

namespace Xharness.Tests
{
    public class AppUninstallerTests
    {
        private const string DeviceName = "Test iPad";
        private const string AppBundleId = "some.bundle.name.app";

        private readonly Mock<IProcessManager> _processManager;
        private readonly Mock<ILog> _mainLog;
        private readonly AppUninstaller _appUninstaller;

        public AppUninstallerTests()
        {
            _mainLog = new Mock<ILog>();

            _processManager = new Mock<IProcessManager>();
            _processManager.SetReturnsDefault(Task.FromResult(new ProcessExecutionResult() { ExitCode = 0 }));

            _appUninstaller = new AppUninstaller(_processManager.Object, _mainLog.Object, 1);
        }

        [Fact]
        public async Task UninstallFromDeviceTest()
        {
            // Act
            var result = await _appUninstaller.UninstallApp(DeviceName, AppBundleId);

            // Verify
            Assert.Equal(0, result.ExitCode);

            var expectedArgs = $"-v -v --uninstalldevbundleid {StringUtils.FormatArguments(AppBundleId)} --devname \"{DeviceName}\"";

            _processManager.Verify(x => x.ExecuteCommandAsync(
               It.Is<MlaunchArguments>(args => args.AsCommandLine() == expectedArgs),
               _mainLog.Object,
               It.IsAny<TimeSpan>(),
               null,
               It.IsAny<CancellationToken>()));
        }
    }
}
