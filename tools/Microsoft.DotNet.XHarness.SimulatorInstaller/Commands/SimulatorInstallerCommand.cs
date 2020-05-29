// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.Common.CLI.Commands;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.SimulatorInstaller.Arguments;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.SimulatorInstaller.Commands
{
    internal abstract class SimulatorInstallerCommand : XHarnessCommand
    {
        private const string MAJOR_VERSION_PLACEHOLDER = "DOWNLOADABLE_VERSION_MAJOR";
        private const string MINOR_VERSION_PLACEHOLDER = "DOWNLOADABLE_VERSION_MINOR";
        private const string VERSION_PLACEHOLDER = "DOWNLOADABLE_VERSION";
        private const string IDENTIFIER_PLACEHOLDER = "DOWNLOADABLE_IDENTIFIER";

        private const string SimulatorIndexUrl = "https://devimages-cdn.apple.com/downloads/xcode/simulators/index-{0}-{1}.dvtdownloadableindex";

        private readonly MacOSProcessManager _processManager = new MacOSProcessManager();

        protected ILogger Logger { get; set; } = null!;

        protected override XHarnessCommandArguments Arguments => SimulatorInstallerArguments;

        protected abstract SimulatorInstallerCommandArguments SimulatorInstallerArguments { get; }

        protected SimulatorInstallerCommand(string name, string help) : base(name, false, help)
        {
        }

        protected static string TempDirectory
        {
            get
            {
                string? path = Path.Combine(Path.GetTempPath(), "simulator-installer");

                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                return path;
            }
        }

        protected async Task<(bool Succeeded, string Stdout)> ExecuteCommand(
            string filename,
            TimeSpan? timeout = null,
            params string[] arguments)
        {
            var stdoutLog = new MemoryLog() { Timestamp = false };
            var stderrLog = new MemoryLog() { Timestamp = false };

            var result = await _processManager.ExecuteCommandAsync(
                filename,
                arguments,
                new CallbackLog(m => Logger.LogDebug(m)),
                stdoutLog,
                stderrLog,
                timeout ?? TimeSpan.FromSeconds(30));

            string stderr = stderrLog.ToString();
            if (stderr.Length > 0)
            {
                Logger.LogDebug("Error output:" + Environment.NewLine + stderr);
            }

            return (result.Succeeded, stdoutLog.ToString());
        }

        protected async Task<IEnumerable<Simulator>> GetAvailableSimulators()
        {
            static string Replace(string value, Dictionary<string, string> replacements)
            {
                foreach (var kvp in replacements)
                {
                    value = value.Replace($"$({kvp.Key})", kvp.Value);
                }

                return value;
            }

            var doc = new XmlDocument();
            doc.LoadXml((await GetSimulatorIndexXml()) ?? throw new FailedToGetIndexException());

            var simulators = new List<Simulator>();

            var downloadables = doc.SelectNodes("//plist/dict/key[text()='downloadables']/following-sibling::array/dict");
            foreach (XmlNode? downloadable in downloadables)
            {
                if (downloadable == null)
                {
                    continue;
                }

                var nameNode = downloadable.SelectSingleNode("key[text()='name']/following-sibling::string");
                var versionNode = downloadable.SelectSingleNode("key[text()='version']/following-sibling::string");
                var sourceNode = downloadable.SelectSingleNode("key[text()='source']/following-sibling::string");
                var identifierNode = downloadable.SelectSingleNode("key[text()='identifier']/following-sibling::string");
                var fileSizeNode = downloadable.SelectSingleNode("key[text()='fileSize']/following-sibling::integer|key[text()='fileSize']/following-sibling::real");
                var installPrefixNode = downloadable.SelectSingleNode("key[text()='userInfo']/following-sibling::dict/key[text()='InstallPrefix']/following-sibling::string");

                var version = versionNode.InnerText;
                var versions = version.Split('.');
                var versionMajor = versions[0];
                var versionMinor = versions[1];
                var dict = new Dictionary<string, string>() {
                    { MAJOR_VERSION_PLACEHOLDER, versionMajor },
                    { MINOR_VERSION_PLACEHOLDER, versionMinor },
                    { VERSION_PLACEHOLDER, version },
                };

                var identifier = Replace(identifierNode.InnerText, dict);

                dict.Add(IDENTIFIER_PLACEHOLDER, identifier);

                double.TryParse(fileSizeNode?.InnerText, out var parsedFileSize);

                simulators.Add(new Simulator(
                    name: Replace(nameNode.InnerText, dict),
                    identifier: Replace(identifierNode.InnerText, dict),
                    version: versionNode.InnerText,
                    source: Replace(sourceNode.InnerText, dict),
                    installPrefix: Replace(installPrefixNode.InnerText, dict),
                    fileSize: (long)parsedFileSize));
            }

            return simulators;
        }

        protected async Task<Version?> IsInstalled(string identifier)
        {
            var (succeeded, pkgInfo) = await ExecuteCommand($"pkgutil", TimeSpan.FromMinutes(1), "--pkg-info", identifier);
            if (!succeeded)
            {
                return null;
            }

            var lines = pkgInfo.Split('\n');
            var version = lines.First(v => v.StartsWith("version: ", StringComparison.Ordinal)).Substring("version: ".Length);
            return Version.Parse(version);
        }

        private async Task<string?> GetSimulatorIndexXml()
        {
            var (xcodeVersion, xcodeUuid) = await GetXcodeInformation();

            var url = string.Format(SimulatorIndexUrl, xcodeVersion, xcodeUuid);
            var uri = new Uri(url);
            var tmpfile = Path.Combine(TempDirectory, Path.GetFileName(uri.LocalPath));

            if (!File.Exists(tmpfile))
            {
                var client = new WebClient();
                try
                {
                    Logger.LogInformation($"Downloading '{uri}'");
                    client.DownloadFile(uri, tmpfile);
                }
                catch (Exception ex)
                {
                    // 403 means 404
                    if (ex is WebException we && (we.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.Forbidden)
                    {
                        Logger.LogWarning($"Failed to download {url}: Not found"); // Apple's servers return a 403 if the file doesn't exist, which can be quite confusing, so show a better error.
                    }
                    else
                    {
                        Logger.LogWarning($"Failed to download {url}: {ex}");
                    }

                    return null;
                }
            }

            var (succeeded, xmlResult) = await ExecuteCommand("plutil", TimeSpan.FromSeconds(30), "-convert", "xml1", "-o", "-", tmpfile);
            if (!succeeded)
            {
                return null;
            }

            return xmlResult;
        }

        private async Task<(string XcodeVersion, string XcodeUuid)> GetXcodeInformation()
        {
            var plistPath = Path.Combine(SimulatorInstallerArguments.XcodeRoot, "Contents", "Info.plist");

            var (succeeded, xcodeVersion) = await ExecuteCommand("/usr/libexec/PlistBuddy", TimeSpan.FromSeconds(5), "-c", "Print :DTXcode", plistPath);
            if (!succeeded)
            {
                throw new Exception("Failed to detect Xcode version!");
            }

            xcodeVersion = xcodeVersion.Trim();

            string xcodeUuid;

            (succeeded, xcodeUuid) = await ExecuteCommand("/usr/libexec/PlistBuddy", TimeSpan.FromSeconds(5), "-c", "Print :DVTPlugInCompatibilityUUID", plistPath);
            if (!succeeded)
            {
                throw new Exception("Failed to detect Xcode UUID!");
            }

            xcodeUuid = xcodeUuid.Trim();

            xcodeVersion = xcodeVersion.Insert(xcodeVersion.Length - 2, ".");
            xcodeVersion = xcodeVersion.Insert(xcodeVersion.Length - 1, ".");

            return (xcodeVersion, xcodeUuid);
        }

        [Serializable]
        protected class FailedToGetIndexException : Exception
        {
            public FailedToGetIndexException() : this("Failed to download the list of available simulators from Apple")
            {
            }

            public FailedToGetIndexException(string? message) : base(message)
            {
            }

            public FailedToGetIndexException(string? message, Exception? innerException) : base(message, innerException)
            {
            }

            protected FailedToGetIndexException(SerializationInfo info, StreamingContext context) : base(info, context)
            {
            }
        }
    }
}
