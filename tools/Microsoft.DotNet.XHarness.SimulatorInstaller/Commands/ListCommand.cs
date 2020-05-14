// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI;
using Microsoft.DotNet.XHarness.SimulatorInstaller.Arguments;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.SimulatorInstaller.Commands
{

    internal class ListCommand : SimulatorInstallerCommand
    {
        protected override string CommandUsage => Name;

        protected override string CommandDescription => "Lists installed simulators";

        private readonly ListCommandArguments _arguments = new ListCommandArguments();
        protected override SimulatorInstallerCommandArguments SimulatorInstallerArguments => _arguments;

        public ListCommand() : base("list", "Lists installed simulators")
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
                if (installedVersion != null)
                {
                    if (installedVersion >= Version.Parse(simulator.Version))
                    {
                        output.AppendLine($" (installed)");
                    }
                    else
                    {
                        output.AppendLine($" (an earlier version is installed: {installedVersion}");
                    }
                }
                else
                {
                    output.AppendLine($" (not installed)");
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
