using System;
using System.IO;
using System.Text;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Tests
{
    public class ExitCodeDetectorTests : IDisposable
    {
        private string _tempFilename = null;

        [Fact]
        public void ExitCodeIsDetectedTest()
        {
            var appBundleInformation = new AppBundleInformation("HelloiOS", "HelloiOS", "some/path", "some/path", false, null);

            var detector = new ExitCodeDetector();

            var log = new[]
            {
                "Nov 18 04:31:44 dci-mac-build-053 CloudKeychainProxy[67121]: objc[67121]: Class MockAKSRefKeyObject is implemented in both /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/Security (0x10350b738) and /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/CloudKeychainProxy.bundle/CloudKeychainProxy (0x103259970). One of the two will be used. Which one is undefined.",
                "Nov 18 04:31:44 dci-mac-build-053 CloudKeychainProxy[67121]: objc[67121]: Class MockAKSOptionalParameters is implemented in both /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/Security (0x10350b788) and /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/CloudKeychainProxy.bundle/CloudKeychainProxy (0x1032599c0). One of the two will be used. Which one is undefined.",
                "Nov 18 04:31:44 dci-mac-build-053 CloudKeychainProxy[67121]: objc[67121]: Class SecXPCHelper is implemented in both /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/Security (0x10350b918) and /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/CloudKeychainProxy.bundle/CloudKeychainProxy (0x103259a60). One of the two will be used. Which one is undefined.",
                "Nov 18 04:31:44 ML-MacVM com.apple.CoreSimulator.SimDevice.2E1EE736-5672-4220-89B5-B7C77DB6AF18[55655] (UIKitApplication:net.dot.HelloiOS[9a0b][rb-legacy][57331]): Service exited with abnormal code: 200",
                "Nov 18 04:31:44 dci-mac-build-053 com.apple.CoreSimulator.SimDevice.F67392D9-A327-4217-B924-5DA0918415E5[811] (com.apple.security.cloudkeychainproxy3[67121]): Service exited with abnormal code: 1",
                "Nov 18 04:31:44 dci-mac-build-053 com.apple.CoreSimulator.SimDevice.F67392D9-A327-4217-B924-5DA0918415E5[811] (com.apple.security.cloudkeychainproxy3): Service only ran for 0 seconds. Pushing respawn out by 10 seconds.",
            };

            var exitCode = detector.DetectExitCode(appBundleInformation, GetLogMock(log));

            Assert.Equal(200, exitCode);
        }

        [Fact]
        public void NegativeExitCodeIsDetectedTest()
        {
            var appBundleInformation = new AppBundleInformation("HelloiOS", "HelloiOS", "some/path", "some/path", false, null);

            var detector = new ExitCodeDetector();

            var log = new[]
            {
                "Nov 18 04:31:44 dci-mac-build-053 CloudKeychainProxy[67121]: objc[67121]: Class MockAKSRefKeyObject is implemented in both /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/Security (0x10350b738) and /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/CloudKeychainProxy.bundle/CloudKeychainProxy (0x103259970). One of the two will be used. Which one is undefined.",
                "Nov 18 04:31:44 dci-mac-build-053 CloudKeychainProxy[67121]: objc[67121]: Class MockAKSOptionalParameters is implemented in both /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/Security (0x10350b788) and /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/CloudKeychainProxy.bundle/CloudKeychainProxy (0x1032599c0). One of the two will be used. Which one is undefined.",
                "Nov 18 04:31:44 dci-mac-build-053 CloudKeychainProxy[67121]: objc[67121]: Class SecXPCHelper is implemented in both /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/Security (0x10350b918) and /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/CloudKeychainProxy.bundle/CloudKeychainProxy (0x103259a60). One of the two will be used. Which one is undefined.",
                "Nov 18 04:31:44 ML-MacVM com.apple.CoreSimulator.SimDevice.2E1EE736-5672-4220-89B5-B7C77DB6AF18[55655] (UIKitApplication:net.dot.HelloiOS[9a0b][rb-legacy][57331]): Service exited with abnormal code: -2",
                "Nov 18 04:31:44 dci-mac-build-053 com.apple.CoreSimulator.SimDevice.F67392D9-A327-4217-B924-5DA0918415E5[811] (com.apple.security.cloudkeychainproxy3[67121]): Service exited with abnormal code: 1",
                "Nov 18 04:31:44 dci-mac-build-053 com.apple.CoreSimulator.SimDevice.F67392D9-A327-4217-B924-5DA0918415E5[811] (com.apple.security.cloudkeychainproxy3): Service only ran for 0 seconds. Pushing respawn out by 10 seconds.",
            };

            var exitCode = detector.DetectExitCode(appBundleInformation, GetLogMock(log));

            Assert.Equal(-2, exitCode);
        }

        [Fact]
        public void ExitCodeIsNotDetectedTest()
        {
            var appBundleInformation = new AppBundleInformation("HelloiOS", "HelloiOS", "some/path", "some/path", false, null);

            var detector = new ExitCodeDetector();

            var log = new[]
            {
                "Nov 18 04:31:44 dci-mac-build-053 CloudKeychainProxy[67121]: objc[67121]: Class MockAKSRefKeyObject is implemented in both /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/Security (0x10350b738) and /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/CloudKeychainProxy.bundle/CloudKeychainProxy (0x103259970). One of the two will be used. Which one is undefined.",
                "Nov 18 04:31:44 dci-mac-build-053 CloudKeychainProxy[67121]: objc[67121]: Class MockAKSOptionalParameters is implemented in both /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/Security (0x10350b788) and /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/CloudKeychainProxy.bundle/CloudKeychainProxy (0x1032599c0). One of the two will be used. Which one is undefined.",
                "Nov 18 04:31:44 dci-mac-build-053 CloudKeychainProxy[67121]: objc[67121]: Class SecXPCHelper is implemented in both /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/Security (0x10350b918) and /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/CloudKeychainProxy.bundle/CloudKeychainProxy (0x103259a60). One of the two will be used. Which one is undefined.",
                "Nov 18 04:31:44 ML-MacVM com.apple.CoreSimulator.SimDevice.2E1EE736-5672-4220-89B5-B7C77DB6AF18[55655] (UIKitApplication:net.dot.HelloiOS[9a0b][rb-legacy][57331]): Some other error message",
                "Nov 18 04:31:44 dci-mac-build-053 com.apple.CoreSimulator.SimDevice.F67392D9-A327-4217-B924-5DA0918415E5[811] (com.apple.security.cloudkeychainproxy3[67121]): Service exited with abnormal code: 1",
                "Nov 18 04:31:44 dci-mac-build-053 com.apple.CoreSimulator.SimDevice.F67392D9-A327-4217-B924-5DA0918415E5[811] (com.apple.security.cloudkeychainproxy3): Service only ran for 0 seconds. Pushing respawn out by 10 seconds.",
            };

            var exitCode = detector.DetectExitCode(appBundleInformation, GetLogMock(log));

            Assert.Equal(0, exitCode);
        }

        [Fact]
        public void ExitCodeFromPreviousRunIsIgnored()
        {
            var previousLog =
                "Nov 18 04:31:44 dci-mac-build-053 CloudKeychainProxy[67121]: objc[67121]: Class MockAKSRefKeyObject is implemented in both /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/Security (0x10350b738) and /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/CloudKeychainProxy.bundle/CloudKeychainProxy (0x103259970). One of the two will be used. Which one is undefined." + Environment.NewLine +
                "Nov 18 04:31:44 dci-mac-build-053 CloudKeychainProxy[67121]: objc[67121]: Class MockAKSOptionalParameters is implemented in both /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/Security (0x10350b788) and /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/CloudKeychainProxy.bundle/CloudKeychainProxy (0x1032599c0). One of the two will be used. Which one is undefined." + Environment.NewLine +
                "Nov 18 04:31:44 dci-mac-build-053 CloudKeychainProxy[67121]: objc[67121]: Class SecXPCHelper is implemented in both /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/Security (0x10350b918) and /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/CloudKeychainProxy.bundle/CloudKeychainProxy (0x103259a60). One of the two will be used. Which one is undefined." + Environment.NewLine +
                "Nov 18 04:31:44 dci-mac-build-053 ML-MacVM com.apple.CoreSimulator.SimDevice.2E1EE736-5672-4220-89B5-B7C77DB6AF18[55655] (UIKitApplication:net.dot.HelloiOS[9a0b][rb-legacy][57331]): Some other error message" + Environment.NewLine +
                "Nov 18 04:31:44 dci-mac-build-053 ML-MacVM com.apple.CoreSimulator.SimDevice.2E1EE736-5672-4220-89B5-B7C77DB6AF18[55655] (UIKitApplication:net.dot.HelloiOS[9a0b][rb-legacy][57331]): Service exited with abnormal code: 55" + Environment.NewLine +
                "Nov 18 04:31:44 dci-mac-build-053 com.apple.CoreSimulator.SimDevice.F67392D9-A327-4217-B924-5DA0918415E5[811] (com.apple.security.cloudkeychainproxy3): Service only ran for 0 seconds. Pushing respawn out by 10 seconds." + Environment.NewLine;

            var currentRunLog =
                "Nov 18 04:31:44 dci-mac-build-053 CloudKeychainProxy[67121]: objc[67121]: Class MockAKSRefKeyObject is implemented in both /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/Security (0x10350b738) and /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/CloudKeychainProxy.bundle/CloudKeychainProxy (0x103259970). One of the two will be used. Which one is undefined." + Environment.NewLine +
                "Nov 18 04:31:44 dci-mac-build-053 CloudKeychainProxy[67121]: objc[67121]: Class MockAKSOptionalParameters is implemented in both /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/Security (0x10350b788) and /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/CloudKeychainProxy.bundle/CloudKeychainProxy (0x1032599c0). One of the two will be used. Which one is undefined." + Environment.NewLine +
                "Nov 18 04:31:44 dci-mac-build-053 CloudKeychainProxy[67121]: objc[67121]: Class SecXPCHelper is implemented in both /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/Security (0x10350b918) and /Applications/Xcode115.app/Contents/Developer/Platforms/iPhoneOS.platform/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/Security.framework/CloudKeychainProxy.bundle/CloudKeychainProxy (0x103259a60). One of the two will be used. Which one is undefined." + Environment.NewLine +
                "Nov 18 04:31:44 dci-mac-build-053 ML-MacVM com.apple.CoreSimulator.SimDevice.2E1EE736-5672-4220-89B5-B7C77DB6AF18[55655] (UIKitApplication:net.dot.HelloiOS[9a0b][rb-legacy][57331]): Some other error message" + Environment.NewLine +
                "Nov 18 04:31:44 dci-mac-build-053 ML-MacVM com.apple.CoreSimulator.SimDevice.2E1EE736-5672-4220-89B5-B7C77DB6AF18[55655] (UIKitApplication:net.dot.HelloiOS[9a0b][rb-legacy][57331]): Service exited with abnormal code: 72" + Environment.NewLine +
                "Nov 18 04:31:44 dci-mac-build-053 com.apple.CoreSimulator.SimDevice.F67392D9-A327-4217-B924-5DA0918415E5[811] (com.apple.security.cloudkeychainproxy3): Service only ran for 0 seconds. Pushing respawn out by 10 seconds." + Environment.NewLine;

            var tempFilename = Path.GetTempFileName();
            File.WriteAllText(tempFilename, previousLog);

            var capturedFilename = Path.GetTempFileName();

            using var captureLog = new CaptureLog(capturedFilename, tempFilename, false);
            captureLog.StartCapture();
            File.AppendAllText(tempFilename, currentRunLog);
            captureLog.StopCapture();

            var appBundleInformation = new AppBundleInformation("net.dot.HelloiOS", "net.dot.HelloiOS", "some/path", "some/path", false, null);
            var exitCode = new ExitCodeDetector().DetectExitCode(appBundleInformation, captureLog);

            Assert.Equal(72, exitCode);
        }

        public void Dispose()
        {
            if (_tempFilename != null)
            {
                File.Delete(_tempFilename);
            }
        }

        private static IFileBackedLog GetLogMock(string[] loglines)
        {
            byte[] byteArray = Encoding.ASCII.GetBytes(string.Join(Environment.NewLine, loglines));
            var stream = new MemoryStream(byteArray);
            var reader = new StreamReader(stream);

            var mock = new Mock<IFileBackedLog>();

            mock.Setup(x => x.GetReader()).Returns(reader);

            return mock.Object;
        }
    }
}
