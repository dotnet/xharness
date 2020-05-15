// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.SimulatorInstaller.Arguments;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.SimulatorInstaller.Commands
{
    internal class ListCommand : SimulatorInstallerCommand
    {
        private const string CommandName = "list";
        private const string CommandHelp = "Lists available simulators";

        protected override string CommandUsage => CommandName;

        protected override string CommandDescription => CommandHelp;

        private readonly ListCommandArguments _arguments = new ListCommandArguments();
        protected override SimulatorInstallerCommandArguments SimulatorInstallerArguments => _arguments;

        public ListCommand() : base(CommandName, CommandHelp)
        {
        }

        protected override async Task<ExitCode> InvokeInternal(ILogger logger)
        {
            Logger = logger;

            var simulators = await GetAvailableSimulators();

            foreach (var simulator in simulators)
            {
                var output = new StringBuilder();
                output.AppendLine(simulator.Name);
                output.Append($"  Version: {simulator.Version}");

                var installedVersion = await IsInstalled(simulator.Identifier);
                if (installedVersion == null)
                {
                    if (_arguments.ListInstalledOnly)
                    {
                        Logger.LogDebug($"The simulator '{simulator.Name}' is not installed");
                        continue;
                    }

                    output.AppendLine($" (not installed)");
                }
                else
                {
                    if (installedVersion >= Version.Parse(simulator.Version))
                    {
                        if (!_arguments.ListInstalledOnly)
                        {
                            output.AppendLine($" (installed)");
                        }
                    }
                    else
                    {
                        output.AppendLine($" (an earlier version is installed: {installedVersion})");
                    }
                }

                output.AppendLine($"  Source: {simulator.Source}");
                output.AppendLine($"  Identifier: {simulator.Identifier}");
                output.AppendLine($"  InstallPrefix: {simulator.InstallPrefix}");

                Logger.LogInformation(output.ToString());
            }

            return ExitCode.SUCCESS;
        }
    }
}
