// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple.Simulators;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Apple.Simulators
{
    internal class ListCommand : SimulatorInstallerCommand
    {
        private const string CommandName = "list";
        private const string CommandHelp = "Lists available simulators";

        protected override string CommandUsage => CommandName;

        protected override string CommandDescription => CommandHelp;

        private readonly ListCommandArguments _arguments = new ListCommandArguments();
        protected override SimulatorsCommandArguments SimulatorsArguments => _arguments;

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

                string? installStatus = null;
                var installedVersion = await IsInstalled(simulator.Identifier);
                if (installedVersion == null)
                {
                    if (_arguments.ListInstalledOnly)
                    {
                        Logger.LogDebug($"The simulator '{simulator.Name}' is not installed");
                        continue;
                    }

                    installStatus = "not installed";
                }
                else
                {
                    if (installedVersion >= Version.Parse(simulator.Version))
                    {
                        if (!_arguments.ListInstalledOnly)
                        {
                            installStatus = "installed";
                        }
                    }
                    else
                    {
                        output.AppendLine();
                        installStatus = $"an earlier version is installed: {installedVersion}";
                    }
                }

                output.AppendLine($" ({installStatus})");
                output.AppendLine($"  Source: {simulator.Source}");
                output.AppendLine($"  Identifier: {simulator.Identifier}");
                output.AppendLine($"  InstallPrefix: {simulator.InstallPrefix}");

                Logger.LogInformation(output.ToString());
            }

            return ExitCode.SUCCESS;
        }
    }
}
