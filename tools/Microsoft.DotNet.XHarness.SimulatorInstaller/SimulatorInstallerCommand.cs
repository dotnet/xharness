// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.CLI.Commands;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.Extensions.Logging;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.SimulatorInstaller
{
    internal abstract class SimulatorInstallerCommandArguments : XHarnessCommandArguments
    {
        private string? _xcodeRoot;

        /// <summary>
        /// Path to where Xcode is located.
        /// </summary>
        public string XcodeRoot
        {
            get
            {
                if (_xcodeRoot == null)
                {
                    // Determine it automatically from xcode-select
                    _xcodeRoot = new ProcessManager().XcodeRoot;
                }

                return _xcodeRoot;
            }
            set => _xcodeRoot = value;
        }

        /// <summary>
        /// How long XHarness should wait until a test execution completes before clean up (kill running apps, uninstall, etc)
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(15);

        protected sealed override OptionSet GetCommandOptions()
        {
            var options = GetAdditionalOptions();

            options.Add("xcode=", "Path to where Xcode is located, e.g. /Application/Xcode114.app. If not set, xcode-select is used to determine the location", v => XcodeRoot = RootPath(v));

            return options;
        }

        protected abstract OptionSet GetAdditionalOptions();

        public override void Validate()
        {
            if (!Directory.Exists(XcodeRoot))
            {
                throw new ArgumentException("Invalid Xcode path supplied");
            }

            var plistPath = Path.Combine(XcodeRoot, "Contents", "Info.plist");
            if (!File.Exists(plistPath))
            {
                throw new ArgumentException($"Cannot find Xcode. The path '{plistPath}' does not exist.");
            }
        }
    }

    internal abstract class SimulatorInstallerCommand : XHarnessCommand
    {
        private readonly IProcessManager _processManager = new ProcessManager();

        protected override XHarnessCommandArguments Arguments => SimulatorInstallerArguments;

        protected abstract SimulatorInstallerCommandArguments SimulatorInstallerArguments { get; }

        protected SimulatorInstallerCommand(string name, string help) : base(name, help)
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
            ILogger logger,
            TimeSpan? timeout = null,
            params string[] arguments)
        {
            var stdoutLog = new MemoryLog() { Timestamp = false };
            var stderrLog = new MemoryLog() { Timestamp = false };

            var result = await _processManager.ExecuteCommandAsync(
                filename,
                arguments,
                new CallbackLog(m => logger.LogDebug(m)),
                stdoutLog,
                stderrLog,
                timeout ?? TimeSpan.FromSeconds(30));

            string stderr = stderrLog.ToString();
            if (stderr.Length > 0)
            {
                logger.LogDebug("Error output: " + stderr);
            }

            return (result.Succeeded, stdoutLog.ToString());
        }
    }
}
