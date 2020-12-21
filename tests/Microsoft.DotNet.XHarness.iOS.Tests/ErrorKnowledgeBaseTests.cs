using System.IO;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Tests
{
    public class ErrorKnowledgeBaseTests
    {
        private readonly ErrorKnowledgeBase _errorKnowledgeBase;

        public ErrorKnowledgeBaseTests()
        {
            _errorKnowledgeBase = new ErrorKnowledgeBase();
        }

        [Fact]
        public void WrongArchPresentTest()
        {
            var logPath = Path.GetTempFileName();
            var expectedFailureMessage =
                "IncorrectArchitecture: Failed to find matching device arch for the application.";
            using (var log = new LogFile("test", logPath))
            {
                // write some data in it
                log.WriteLine("InstallingEmbeddedProfile: 65%");
                log.WriteLine("PercentComplete: 30");
                log.WriteLine("Status: InstallingEmbeddedProfile");
                log.WriteLine("VerifyingApplication: 70%");
                log.WriteLine("PercentComplete: 40");
                log.WriteLine("Status: VerifyingApplication");
                log.WriteLine(
                    "IncorrectArchitecture: Failed to find matching arch for 64-bit Mach-O input file /private/var/installd/Library/Caches/com.apple.mobile.installd.staging/temp.Ic8Ank/extracted/monotouchtest.app/monotouchtest");
                log.Flush();

                Assert.True(_errorKnowledgeBase.IsKnownInstallIssue(log, out var failureMessage));
                Assert.Equal(expectedFailureMessage, failureMessage.Value.HumanMessage);
            }

            if (File.Exists(logPath))
            {
                File.Delete(logPath);
            }
        }

        [Fact]
        public void WrongArchNotPresentTest()
        {
            var logPath = Path.GetTempFileName();
            using (var log = new LogFile("test", logPath))
            {
                // write some data in it
                log.WriteLine("InstallingEmbeddedProfile: 65%");
                log.WriteLine("PercentComplete: 30");
                log.WriteLine("Status: InstallingEmbeddedProfile");
                log.WriteLine("VerifyingApplication: 70%");
                log.WriteLine("PercentComplete: 40");
                log.WriteLine("Status: VerifyingApplication");
                log.Flush();

                Assert.False(_errorKnowledgeBase.IsKnownInstallIssue(log, out var failureMessage));
                Assert.Null(failureMessage);
            }

            if (File.Exists(logPath))
            {
                File.Delete(logPath);
            }
        }

        [Fact]
        public void UsbIssuesPresentTest()
        {
            var expectedFailureMessage =
                "Failed to communicate with the device. Please ensure the cable is properly connected, and try rebooting the device";
            var logPath = Path.GetTempFileName();
            using (var log = new LogFile("test", logPath))
            {
                // initial lines are not intereting
                log.WriteLine("InstallingEmbeddedProfile: 65%");
                log.WriteLine("PercentComplete: 30");
                log.WriteLine("Status: InstallingEmbeddedProfile");
                log.WriteLine("VerifyingApplication: 70%");
                log.WriteLine("PercentComplete: 40");
                log.WriteLine("Xamarin.Hosting.MobileDeviceException: Failed to communicate with the device. Please ensure the cable is properly connected, and try rebooting the device (error: 0xe8000065 kAMDMuxConnectError)");
                log.Flush();
                Assert.True(_errorKnowledgeBase.IsKnownTestIssue(log, out var failureMessage));
                Assert.Equal(expectedFailureMessage, failureMessage.Value.HumanMessage);
            }
            if (File.Exists(logPath))
            {
                File.Delete(logPath);
            }
        }

        [Fact]
        public void UsbIssuesMissintTest()
        {
            var logPath = Path.GetTempPath();
            using (var log = new LogFile("test", logPath))
            {
                // initial lines are not intereting
                log.WriteLine("InstallingEmbeddedProfile: 65%");
                log.WriteLine("PercentComplete: 30");
                log.WriteLine("Status: InstallingEmbeddedProfile");
                log.WriteLine("VerifyingApplication: 70%");
                log.WriteLine("PercentComplete: 40");
                log.Flush();
                Assert.False(_errorKnowledgeBase.IsKnownTestIssue(log, out var failureMessage));
                Assert.Null(failureMessage);
            }
            if (File.Exists(logPath))
            {
                File.Delete(logPath);
            }
        }
    }
}
