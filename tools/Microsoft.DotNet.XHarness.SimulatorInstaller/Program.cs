// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.Extensions.Logging;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.SimulatorInstaller
{
    /// <summary>
    /// This command line tool allows management of Xcode iOS/WatchOS/tvOS Simulators on MacOS.
    /// It is used for automated update of OSX servers.
    /// Originally taken from: https://github.com/xamarin/xamarin-macios/blob/master/tools/siminstaller/Program.cs
    /// </summary>
    public static class Program
    {
        private static readonly ProcessManager s_processManager = new ProcessManager();
        private static ILogger? s_logger = null!;

        private static bool s_printSimulators;
        private static int s_verbose;

        private static string TempDirectory
        {
            get
            {
                string? path = Path.Combine(Path.GetTempPath(), "x-provisioning");

                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                return path;
            }
        }

        private static async Task<(bool Succeeded, string Stdout)> ExecuteCommand(string filename, TimeSpan? timeout = null, params string[] arguments)
        {
            var stdoutLog = new MemoryLog() { Timestamp = false };
            var stderrLog = new MemoryLog() { Timestamp = false };

            var result = await s_processManager.ExecuteCommandAsync(filename, arguments, new CallbackLog(m => s_logger.LogDebug(m)), stdoutLog, stderrLog, timeout ?? TimeSpan.FromSeconds(30));

            string stderr = stderrLog.ToString();
            if (stderr.Length > 0)
            {
                s_logger.LogWarning("Error output: " + stderr);
            }

            return (result.Succeeded, stdoutLog.ToString());
        }

        public static async Task<int> Main(string[] args)
        {
            var exit_code = 0;
            string? xcode_app = null;
            var simulatorsToInstall = new List<string>();
            var checkOnly = false;
            var force = false;
            var os = new OptionSet
            {
                { "xcode=", "Path to where Xcode is located, e.g. /Application/Xcode114.app", v => xcode_app = v },
                { "install=", "ID of simulator to install. Can be repeated multiple times.", v => simulatorsToInstall.Add (v) },
                { "only-check", "Only check if the simulators are installed or not. Prints the name of any missing simulators, and returns 1 if any non-installed simulators were found.", v => checkOnly = true },
                { "print-simulators", "Print all detected simulators.", v => s_printSimulators = true },
                { "f|force", "Install again even if already installed.", v => force = true },
                { "v|verbose", "Increase verbosity", v => s_verbose++ },
            };

            var others = os.Parse(args);

            s_logger = CreateLoggerFactory(s_verbose > 0 ? LogLevel.Debug : LogLevel.Information).CreateLogger("SimulatorInstaller");

            if (others.Count() > 0)
            {
                s_logger.LogError("Unexpected arguments:");
                foreach (var arg in others)
                {
                    s_logger.LogError("\t{0}", arg);
                }

                return 1;
            }

            if (string.IsNullOrEmpty(xcode_app))
            {
                s_logger.LogError("--xcode is required.");
                return 1;
            }
            else if (!Directory.Exists(xcode_app))
            {
                s_logger.LogError("The Xcode directory {0} does not exist.", xcode_app);
                return 1;
            }

            var plistPath = Path.Combine(xcode_app, "Contents", "Info.plist");
            if (!File.Exists(plistPath))
            {
                s_logger.LogError($"'{plistPath}' does not exist.");
                return 1;
            }

            var (succeeded, xcodeVersion) = await ExecuteCommand("/usr/libexec/PlistBuddy", TimeSpan.FromSeconds(5), "-c", "Print :DTXcode", plistPath);

            if (!succeeded)
            {
                return 1;
            }

            xcodeVersion = xcodeVersion.Trim();

            string xcodeUuid;

            (succeeded, xcodeUuid) = await ExecuteCommand("/usr/libexec/PlistBuddy", TimeSpan.FromSeconds(5), "-c", "Print :DVTPlugInCompatibilityUUID", plistPath);
            if (!succeeded)
            {
                return 1;
            }

            xcodeUuid = xcodeUuid.Trim();

            xcodeVersion = xcodeVersion.Insert(xcodeVersion.Length - 2, ".");
            xcodeVersion = xcodeVersion.Insert(xcodeVersion.Length - 1, ".");
            var url = $"https://devimages-cdn.apple.com/downloads/xcode/simulators/index-{xcodeVersion}-{xcodeUuid}.dvtdownloadableindex";
            var uri = new Uri(url);
            var tmpfile = Path.Combine(TempDirectory, Path.GetFileName(uri.LocalPath));
            if (!File.Exists(tmpfile))
            {
                var client = new WebClient();
                try
                {
                    s_logger.LogInformation($"Downloading '{uri}'");
                    client.DownloadFile(uri, tmpfile);
                }
                catch (Exception ex)
                {
                    // 403 means 404
                    if (ex is WebException we && (we.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.Forbidden)
                    {
                        s_logger.LogWarning($"Failed to download {url}: Not found"); // Apple's servers return a 403 if the file doesn't exist, which can be quite confusing, so show a better error.
                    }
                    else
                    {
                        s_logger.LogWarning($"Failed to download {url}: {ex}");
                    }

                    // We couldn't download the list of simulators, but the simulator(s) we were requested to install might already be installed.
                    // Don't fail in that case (we'd miss any potential updates, but that's probably not too bad).
                    if (simulatorsToInstall.Any())
                    {
                        s_logger.LogDebug("Checking if all the requested simulators are already installed");

                        foreach (var name in simulatorsToInstall)
                        {
                            if ((await IsInstalled(name)) == null)
                            {
                                s_logger.LogError($"The simulator '{name}' is not installed.");
                                exit_code = 1;
                            }
                            else
                            {
                                s_logger.LogInformation($"The simulator '{name}' is installed.");
                            }
                        }
                        // We can't install any missing simulators, because we don't have the download url (since we couldn't download the .dvtdownloadableindex file), so just exit.
                        return exit_code;
                    }
                    return 1;
                }
            }

            string xmlResult;
            (succeeded, xmlResult) = await ExecuteCommand("plutil", TimeSpan.FromSeconds(30), "-convert", "xml1", "-o", "-", tmpfile);
            if (!succeeded)
            {
                return 1;
            }

            var doc = new XmlDocument();
            doc.LoadXml(xmlResult);

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
                    { "DOWNLOADABLE_VERSION_MAJOR", versionMajor },
                    { "DOWNLOADABLE_VERSION_MINOR", versionMinor },
                    { "DOWNLOADABLE_VERSION", version },
                };

                var identifier = Replace(identifierNode.InnerText, dict);

                dict.Add("DOWNLOADABLE_IDENTIFIER", identifier);

                var name = Replace(nameNode.InnerText, dict);
                var source = Replace(sourceNode.InnerText, dict);
                var installPrefix = Replace(installPrefixNode.InnerText, dict);
                double.TryParse(fileSizeNode?.InnerText, out var parsedFileSize);
                var fileSize = (long)parsedFileSize;

                var installed = false;
                var updateAvailable = false;

                if (checkOnly && !simulatorsToInstall.Contains(identifier))
                {
                    continue;
                }

                var installedVersion = await IsInstalled(identifier);
                if (installedVersion != null)
                {
                    if (installedVersion >= Version.Parse(version))
                    {
                        installed = true;
                    }
                    else
                    {
                        updateAvailable = true;
                    }
                }

                var doInstall = false;
                if (simulatorsToInstall.Contains(identifier))
                {
                    if (force)
                    {
                        doInstall = true;
                        if (!checkOnly && s_verbose >= 0 && installed)
                        {
                            s_logger.LogInformation($"The simulator '{identifier}' is already installed, but will be installed again because --force was specified.");
                        }
                    }
                    else if (installed)
                    {
                        if (!checkOnly && s_verbose >= 0)
                        {
                            s_logger.LogInformation($"Not installing '{identifier}' because it's already installed and up-to-date.");
                        }
                    }
                    else
                    {
                        doInstall = true;
                    }
                    simulatorsToInstall.Remove(identifier);
                }

                if (s_printSimulators)
                {
                    var output = new StringBuilder();
                    output.AppendLine(name);
                    output.Append($"  Version: {version}");
                    if (updateAvailable)
                    {
                        output.AppendLine($" (an earlier version is installed: {installedVersion}");
                    }
                    else if (!installed)
                    {
                        output.AppendLine($" (not installed)");
                    }
                    else
                    {
                        output.AppendLine($" (installed)");
                    }

                    output.AppendLine($"  Source: {source}");
                    output.AppendLine($"  Identifier: {identifier}");
                    output.AppendLine($"  InstallPrefix: {installPrefix}");

                    s_logger.LogInformation(output.ToString());
                }

                if (checkOnly)
                {
                    if (doInstall)
                    {
                        if (updateAvailable)
                        {
                            s_logger.LogInformation(s_verbose > 0 ? $"The simulator '{name}' is installed, but an update is available." : name);
                        }
                        else
                        {
                            s_logger.LogInformation(s_verbose > 0 ? $"The simulator '{name}' is not installed." : name);
                        }
                        exit_code = 1;
                    }
                    else
                    {
                        s_logger.LogInformation($"The simulator '{name}' is installed.");
                    }
                }
                if (doInstall && !checkOnly)
                {
                    s_logger.LogInformation($"Installing {name}...");
                    if (await Install(source, fileSize, installPrefix))
                    {
                        s_logger.LogInformation($"Installed {name} successfully.");
                    }
                    else
                    {
                        s_logger.LogError($"Failed to install {name}.");
                        return 1;
                    }
                }
            }

            if (simulatorsToInstall.Count > 0)
            {
                s_logger.LogError("Unknown simulators: {0}", string.Join(", ", simulatorsToInstall));
                return 1;
            }

            return exit_code;
        }

        private static async Task<Version?> IsInstalled(string identifier)
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

        private static async Task<bool> Install(string source, long fileSize, string installPrefix)
        {
            var filename = Path.GetFileName(source);
            var downloadPath = Path.Combine(TempDirectory, filename);
            var download = true;

            if (!File.Exists(downloadPath))
            {
                s_logger.LogInformation($"Downloading '{source}' to '{downloadPath}' (size: {fileSize} bytes = {fileSize / 1024.0 / 1024.0:N2} MB)...");
            }
            else if (new FileInfo(downloadPath).Length != fileSize)
            {
                s_logger.LogInformation($"Downloading '{source}' to '{downloadPath}' because the existing file's size {new FileInfo(downloadPath).Length} does not match the expected size {fileSize}...");
            }
            else
            {
                download = false;
            }

            if (download)
            {
                var downloadDone = new ManualResetEvent(false);
                var wc = new WebClient();
                long lastProgress = 0;
                var watch = Stopwatch.StartNew();
                wc.DownloadProgressChanged += (sender, progressArgs) =>
                {
                    var progress = progressArgs.BytesReceived * 100 / fileSize;
                    if (progress > lastProgress)
                    {
                        lastProgress = progress;
                        var duration = watch.Elapsed.TotalSeconds;
                        var speed = progressArgs.BytesReceived / duration;
                        var timeLeft = TimeSpan.FromSeconds((progressArgs.TotalBytesToReceive - progressArgs.BytesReceived) / speed);
                        s_logger.LogDebug($"Downloaded {progressArgs.BytesReceived:N0}/{fileSize:N0} bytes = {progress}% in {duration:N1}s ({speed / 1024.0 / 1024.0:N1} MB/s; approximately {new TimeSpan(timeLeft.Days, timeLeft.Hours, timeLeft.Minutes, timeLeft.Seconds)} left)");
                    }
                };

                wc.DownloadFileCompleted += (sender, completedArgs) =>
                {
                    s_logger.LogInformation($"Download completed in {watch.Elapsed.TotalSeconds}s");
                    if (completedArgs.Error != null)
                    {
                        s_logger.LogError($"    with error: {completedArgs.Error}");
                    }
                    downloadDone.Set();
                };

                await wc.DownloadFileTaskAsync(new Uri(source), downloadPath);

                downloadDone.WaitOne();
            }

            var mount_point = Path.Combine(TempDirectory, filename + "-mount");
            Directory.CreateDirectory(mount_point);
            try
            {
                s_logger.LogInformation($"Mounting '{downloadPath}' into '{mount_point}'...");
                var (succeeded, stdout) = await ExecuteCommand("hdiutil", TimeSpan.FromMinutes(1), "attach", downloadPath, "-mountpoint", mount_point, "-quiet", "-nobrowse");
                if (!succeeded)
                {
                    s_logger.LogError("Mount failure!" + Environment.NewLine + stdout);
                    return false;
                }

                try
                {
                    var packages = Directory.GetFiles(mount_point, "*.pkg");
                    if (packages.Length == 0)
                    {
                        s_logger.LogError("Found no *.pkg files in the dmg.");
                        return false;
                    }
                    else if (packages.Length > 1)
                    {
                        s_logger.LogError("Found more than one *.pkg file in the dmg:\n\t{0}", string.Join("\n\t", packages));
                        return false;
                    }

                    // According to the package manifest, the package's install location is /.
                    // That's obviously not where it's installed, but I have no idea how Apple does it
                    // So instead decompress the package, modify the package manifest, re-create the package, and then install it.
                    var expanded_path = Path.Combine(TempDirectory + "-expanded-pkg");
                    if (Directory.Exists(expanded_path))
                    {
                        Directory.Delete(expanded_path, true);
                    }

                    s_logger.LogInformation($"Expanding '{packages[0]}' into '{expanded_path}'...");
                    (succeeded, stdout) = await ExecuteCommand("pkgutil", TimeSpan.FromMinutes(1), "--expand", packages[0], expanded_path);
                    if (!succeeded)
                    {
                        s_logger.LogError($"Failed to expand {packages[0]}:" + Environment.NewLine + stdout);
                        return false;
                    }

                    try
                    {
                        var packageInfoPath = Path.Combine(expanded_path, "PackageInfo");
                        var packageInfoDoc = new XmlDocument();
                        packageInfoDoc.Load(packageInfoPath);
                        // Add the install-location attribute to the pkg-info node
                        var attr = packageInfoDoc.CreateAttribute("install-location");
                        attr.Value = installPrefix;
                        packageInfoDoc.SelectSingleNode("/pkg-info").Attributes.Append(attr);
                        packageInfoDoc.Save(packageInfoPath);

                        var fixed_path = Path.Combine(Path.GetDirectoryName(downloadPath)!, Path.GetFileNameWithoutExtension(downloadPath) + "-fixed.pkg");
                        if (File.Exists(fixed_path))
                        {
                            File.Delete(fixed_path);
                        }

                        try
                        {
                            s_logger.LogInformation($"Creating fixed package '{fixed_path}' from '{expanded_path}'...");

                            (succeeded, stdout) = await ExecuteCommand("pkgutil", TimeSpan.FromMinutes(2), "--flatten", expanded_path, fixed_path);
                            if (!succeeded)
                            {
                                s_logger.LogError("Failed to create fixed package:" + Environment.NewLine + stdout);
                                return false;
                            }

                            s_logger.LogInformation($"Installing '{fixed_path}'...");
                            (succeeded, stdout) = await ExecuteCommand("sudo", TimeSpan.FromMinutes(15), "installer", "-pkg", fixed_path, "-target", "/", "-verbose", "-dumplog");
                            if (!succeeded)
                            {
                                s_logger.LogError("Failed to install package:" + Environment.NewLine + stdout);
                                return false;
                            }
                        }
                        finally
                        {
                            if (File.Exists(fixed_path))
                            {
                                File.Delete(fixed_path);
                            }
                        }
                    }
                    finally
                    {
                        Directory.Delete(expanded_path, true);
                    }
                }
                finally
                {
                    await ExecuteCommand("hdiutil", TimeSpan.FromMinutes(5), "detach", mount_point, "-quiet");
                }
            }
            finally
            {
                Directory.Delete(mount_point, true);
            }

            File.Delete(downloadPath);

            return true;
        }

        private static string Replace(string value, Dictionary<string, string> replacements)
        {
            foreach (var kvp in replacements)
            {
                value = value.Replace($"$({kvp.Key})", kvp.Value);
            }

            return value;
        }

        private static ILoggerFactory CreateLoggerFactory(LogLevel verbosity) => LoggerFactory.Create(builder =>
            builder
                .AddConsole(options => options.TimestampFormat = "[HH:mm:ss] ")
                .AddFilter(level => level >= verbosity));
    }
}
