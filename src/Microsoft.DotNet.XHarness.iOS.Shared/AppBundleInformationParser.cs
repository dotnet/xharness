// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;

namespace Microsoft.DotNet.XHarness.iOS.Shared
{
    public interface IAppBundleInformationParser
    {
        AppBundleInformation ParseFromProject(string projectFilePath, TestTarget target, string buildConfiguration);

        Task<AppBundleInformation> ParseFromAppBundle(string appPackagePath, TestTarget target, ILog log, CancellationToken cancellationToken = default);
    }

    public class AppBundleInformationParser : IAppBundleInformationParser
    {
        private const string PlistBuddyPath = "/usr/libexec/PlistBuddy";
        private const string Armv7 = "armv7";

        private readonly IProcessManager _processManager;

        public AppBundleInformationParser(IProcessManager processManager)
        {
            _processManager = processManager ?? throw new System.ArgumentNullException(nameof(processManager));
        }

        public AppBundleInformation ParseFromProject(string projectFilePath, TestTarget target, string buildConfiguration)
        {
            var csproj = new XmlDocument();
            csproj.LoadWithoutNetworkAccess(projectFilePath);

            string appName = csproj.GetAssemblyName();
            string info_plist_path = csproj.GetInfoPListInclude();

            var info_plist = new XmlDocument();
            string plistPath = Path.Combine(Path.GetDirectoryName(projectFilePath), info_plist_path.Replace('\\', Path.DirectorySeparatorChar));
            info_plist.LoadWithoutNetworkAccess(plistPath);

            string bundleIdentifier = info_plist.GetCFBundleIdentifier();

            Extension? extension = null;
            string extensionPointIdentifier = info_plist.GetNSExtensionPointIdentifier();
            if (!string.IsNullOrEmpty(extensionPointIdentifier))
                extension = extensionPointIdentifier.ParseFromNSExtensionPointIdentifier();

            var platform = target.IsSimulator() ? "iPhoneSimulator" : "iPhone";

            string appPath = Path.Combine(Path.GetDirectoryName(projectFilePath),
                csproj.GetOutputPath(platform, buildConfiguration).Replace('\\', Path.DirectorySeparatorChar),
                appName + (extension != null ? ".appex" : ".app"));

            string arch = csproj.GetMtouchArch(platform, buildConfiguration);
            bool supports32 = arch.Contains("ARMv7", StringComparison.InvariantCultureIgnoreCase) || arch.Contains("i386", StringComparison.InvariantCultureIgnoreCase);

            if (!Directory.Exists(appPath))
                throw new DirectoryNotFoundException($"The app bundle directory `{appPath}` does not exist");

            string launchAppPath = target.ToRunMode() == RunMode.WatchOS
                ? Directory.GetDirectories(Path.Combine(appPath, "Watch"), "*.app")[0]
                : appPath;

            return new AppBundleInformation(appName, bundleIdentifier, appPath, launchAppPath, supports32, extension);
        }

        public async Task<AppBundleInformation> ParseFromAppBundle(string appPackagePath, TestTarget target, ILog log, CancellationToken cancellationToken = default)
        {
            var plistPath = Path.Join(appPackagePath, "Info.plist");

            if (!File.Exists(plistPath))
            {
                throw new Exception($"Failed to find Info.plist inside the app bundle at: '{plistPath}'");
            }

            var appName = await GetPlistProperty(plistPath, PListExtensions.BundleNamePropertyName, log, cancellationToken);
            var bundleIdentifier = await GetPlistProperty(plistPath, PListExtensions.BundleIdentifierPropertyName, log, cancellationToken);

            string supports32 = string.Empty;

            try
            {
                supports32 = await GetPlistProperty(plistPath, PListExtensions.RequiredDeviceCapabilities, log, cancellationToken);
            }
            catch
            {
                // The property might not be present
                log.WriteLine("Failed to get the UIRequiredDeviceCapabilities Info.plist property. Assumes 32 bit is not supported");
            }

            string launchAppPath = target.ToRunMode() == RunMode.WatchOS
                ? Directory.GetDirectories(Path.Combine(appPackagePath, "Watch"), "*.app")[0]
                : appPackagePath;

            return new AppBundleInformation(appName, appPackagePath, bundleIdentifier, launchAppPath, supports32.Contains(Armv7, StringComparison.InvariantCultureIgnoreCase), extension: null);
        }

        private async Task<string> GetPlistProperty(string plistPath, string propertyName, ILog log, CancellationToken cancellationToken = default)
        {
            var args = new[]
            {
                "-c",
                $"Print {propertyName}",
                plistPath,
            };

            var commandOutput = new MemoryLog { Timestamp = false };
            var result = await _processManager.ExecuteCommandAsync(PlistBuddyPath, args, log, commandOutput, commandOutput, TimeSpan.FromSeconds(15), cancellationToken: cancellationToken);

            if (!result.Succeeded)
            {
                throw new Exception($"Failed to get bundle information: {commandOutput}");
            }

            return commandOutput.ToString().Trim();
        }
    }
}
