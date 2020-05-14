// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI;
using Microsoft.DotNet.XHarness.SimulatorInstaller.Arguments;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.SimulatorInstaller.Commands
{
    internal class FindCommand : SimulatorInstallerCommand
    {
        protected override string CommandUsage => "find [OPTIONS]";

        private const string _commandDescription = "Finds whether given simulators are installed and outputs list of missing ones (returns 0 when none missing)";
        protected override string CommandDescription => _commandDescription;

        private readonly FindCommandArguments _arguments = new FindCommandArguments();
        protected override SimulatorInstallerCommandArguments SimulatorInstallerArguments => _arguments;

        public FindCommand() : base("find", _commandDescription)
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

            // We output a list of simulators that were supplied and not installed
            foreach (var simulator in simulators)
            {
                var installedVersion = await IsInstalled(simulator.Identifier);

                if (installedVersion == null && _arguments.Simulators.Any(identifier => simulator.Identifier.Equals(identifier, StringComparison.InvariantCultureIgnoreCase)))
                {
                    if (_arguments.Verbosity == LogLevel.Debug)
                    {
                        Logger.LogDebug($"The simulator '{simulator.Name}' is not installed");

                    } else
                    {
                        // For parsing
                        Console.WriteLine(simulator.Identifier);
                    }

                    exitCode = ExitCode.DEVICE_NOT_FOUND;
                    continue;
                }

                if (installedVersion >= Version.Parse(simulator.Version))
                {
                    Logger.LogDebug($"The simulator {simulator.Version} is installed ({simulator.Version})");
                }
                else
                {
                    Logger.LogDebug($"The simulator {simulator.Name} is installed, but an update is available ({simulator.Version}).");
                }
            }

            return exitCode;
        }
    }
}
