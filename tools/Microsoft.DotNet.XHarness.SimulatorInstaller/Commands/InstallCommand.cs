// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.SimulatorInstaller.Arguments;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.SimulatorInstaller.Commands
{
    internal class InstallCommand : SimulatorInstallerCommand
    {
        private const string CommandName = "install";
        private const string CommandHelp = "Installs given simulators";

        protected override string CommandUsage => CommandName + " [OPTIONS]";

        protected override string CommandDescription => CommandHelp;

        private readonly InstallCommandArguments _arguments = new InstallCommandArguments();
        protected override SimulatorInstallerCommandArguments SimulatorInstallerArguments => _arguments;

        public InstallCommand() : base(CommandName, CommandHelp)
        {
        }

        protected override async Task<ExitCode> InvokeInternal(ILogger logger)
        {
            Logger = logger;

            var simulators = await GetAvailableSimulators();
            var exitCode = ExitCode.SUCCESS;

            var unknownSimulators = _arguments.Simulators.Where(identifier =>
                !simulators.Any(sim => sim.Identifier.Equals(identifier, StringComparison.InvariantCultureIgnoreCase)));

            if (unknownSimulators.Any())
            {
                Logger.LogError("Unknown simulators: " + string.Join(", ", unknownSimulators));
                return ExitCode.DEVICE_NOT_FOUND;
            }

            foreach (var simulator in simulators)
            {
                if (!_arguments.Simulators.Any(identifier => simulator.Identifier.Equals(identifier, StringComparison.InvariantCultureIgnoreCase)))
                {
                    Logger.LogDebug($"Skipping '{simulator.Identifier}'");
                    continue;
                }

                var installedVersion = await IsInstalled(simulator.Identifier);
                var shouldInstall = false;

                if (installedVersion == null)
                {
                    Logger.LogInformation($"The simulator '{simulator.Name}' is missing and will be installed");
                    shouldInstall = true;
                }
                else
                {
                    if (installedVersion >= Version.Parse(simulator.Version))
                    {
                        if (_arguments.Force)
                        {
                            Logger.LogInformation($"The simulator '{simulator.Name}' is installed but --force was supplied so reinstalling");
                            shouldInstall = true;
                        }
                        else
                        {
                            Logger.LogInformation($"The simulator '{simulator.Name}' is already installed ({simulator.Version})");
                        }
                    }
                    else
                    {
                        Logger.LogInformation($"The simulator '{simulator.Name}' is installed, but an update is available ({simulator.Version}).");
                    }
                }

                if (shouldInstall)
                {
                    Logger.LogInformation($"Installing '{simulator.Name}' ({simulator.Version})...");

                    try
                    {
                        if (await Install(simulator))
                        {
                            Logger.LogInformation($"Installed '{simulator.Name}' successfully");
                        }
                        else
                        {
                            Logger.LogError($"Failed to install '{simulator.Name}'");
                            exitCode = ExitCode.GENERAL_FAILURE;
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.LogError($"Failed to install '{simulator.Name}':{Environment.NewLine}{e}");
                        exitCode = ExitCode.GENERAL_FAILURE;
                    }
                }
            }

            return exitCode;
        }

        private async Task<bool> Install(Simulator simulator)
        {
            var filename = Path.GetFileName(simulator.Source);
            var downloadPath = Path.Combine(TempDirectory, filename);
            var download = true;

            if (!File.Exists(downloadPath))
            {
                Logger.LogInformation(
                    $"Downloading '{simulator.Source}' to '{downloadPath}' " +
                    $"(size: {simulator.FileSize} bytes = {simulator.FileSize / 1024.0 / 1024.0:N2} MB)...");
            }
            else if (new FileInfo(downloadPath).Length != simulator.FileSize)
            {
                Logger.LogInformation(
                    $"Downloading '{simulator.Source}' to '{downloadPath}' because the existing file's " +
                    $"size {new FileInfo(downloadPath).Length} does not match the expected size {simulator.FileSize}...");
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
                    var progress = progressArgs.BytesReceived * 100 / simulator.FileSize;
                    if (progress > lastProgress)
                    {
                        lastProgress = progress;
                        var duration = watch.Elapsed.TotalSeconds;
                        var speed = progressArgs.BytesReceived / duration;
                        var timeLeft = TimeSpan.FromSeconds((progressArgs.TotalBytesToReceive - progressArgs.BytesReceived) / speed);

                        Logger.LogDebug($"Downloaded {progressArgs.BytesReceived:N0}/{simulator.FileSize:N0} bytes = " +
                            $"{progress}% in {duration:N1}s ({speed / 1024.0 / 1024.0:N1} MB/s; approximately " +
                            $"{new TimeSpan(timeLeft.Days, timeLeft.Hours, timeLeft.Minutes, timeLeft.Seconds)} left)");
                    }
                };

                wc.DownloadFileCompleted += (sender, completedArgs) =>
                {
                    Logger.LogInformation($"Download completed in {watch.Elapsed.TotalSeconds}s");

                    if (completedArgs.Error != null)
                    {
                        Logger.LogError($"    with error: {completedArgs.Error}");
                    }

                    downloadDone.Set();
                };

                await wc.DownloadFileTaskAsync(new Uri(simulator.Source), downloadPath);

                downloadDone.WaitOne();
            }

            var mount_point = Path.Combine(TempDirectory, filename + "-mount");
            Directory.CreateDirectory(mount_point);
            try
            {
                Logger.LogInformation($"Mounting '{downloadPath}' into '{mount_point}'...");
                var (succeeded, stdout) = await ExecuteCommand("hdiutil", TimeSpan.FromMinutes(1), "attach", downloadPath, "-mountpoint", mount_point, "-quiet", "-nobrowse");
                if (!succeeded)
                {
                    Logger.LogError("Mount failure!" + Environment.NewLine + stdout);
                    return false;
                }

                try
                {
                    var packages = Directory.GetFiles(mount_point, "*.pkg");
                    if (packages.Length == 0)
                    {
                        Logger.LogError("Found no *.pkg files in the dmg.");
                        return false;
                    }
                    else if (packages.Length > 1)
                    {
                        Logger.LogError("Found more than one *.pkg file in the dmg:\n\t{0}", string.Join("\n\t", packages));
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

                    Logger.LogInformation($"Expanding '{packages[0]}' into '{expanded_path}'...");
                    (succeeded, stdout) = await ExecuteCommand("pkgutil", TimeSpan.FromMinutes(1), "--expand", packages[0], expanded_path);
                    if (!succeeded)
                    {
                        Logger.LogError($"Failed to expand {packages[0]}:" + Environment.NewLine + stdout);
                        return false;
                    }

                    try
                    {
                        var packageInfoPath = Path.Combine(expanded_path, "PackageInfo");
                        var packageInfoDoc = new XmlDocument();
                        packageInfoDoc.Load(packageInfoPath);
                        // Add the install-location attribute to the pkg-info node
                        var attr = packageInfoDoc.CreateAttribute("install-location");
                        attr.Value = simulator.InstallPrefix;
                        packageInfoDoc.SelectSingleNode("/pkg-info").Attributes.Append(attr);
                        packageInfoDoc.Save(packageInfoPath);

                        var fixed_path = Path.Combine(Path.GetDirectoryName(downloadPath)!, Path.GetFileNameWithoutExtension(downloadPath) + "-fixed.pkg");
                        if (File.Exists(fixed_path))
                        {
                            File.Delete(fixed_path);
                        }

                        try
                        {
                            Logger.LogInformation($"Creating fixed package '{fixed_path}' from '{expanded_path}'...");

                            (succeeded, stdout) = await ExecuteCommand("pkgutil", TimeSpan.FromMinutes(2), "--flatten", expanded_path, fixed_path);
                            if (!succeeded)
                            {
                                Logger.LogError("Failed to create fixed package:" + Environment.NewLine + stdout);
                                return false;
                            }

                            Logger.LogInformation($"Installing '{fixed_path}'...");
                            (succeeded, stdout) = await ExecuteCommand("sudo", TimeSpan.FromMinutes(15), "installer", "-pkg", fixed_path, "-target", "/", "-verbose", "-dumplog");
                            if (!succeeded)
                            {
                                Logger.LogError("Failed to install package:" + Environment.NewLine + stdout);
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
    }
}
