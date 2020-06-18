// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.SimulatorInstaller.Commands;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.SimulatorInstaller
{
    internal class CustomHelpCommand : HelpCommand
    {
        public override int Invoke(IEnumerable<string> arguments)
        {
            string[] args = arguments.ToArray();

            if (args.Length == 0)
            {
                base.Invoke(arguments);
                return (int)ExitCode.HELP_SHOWN;
            }

            var command = args[0].ToLowerInvariant();
            PrintCommandHelp(command);

            return (int)ExitCode.HELP_SHOWN;
        }

        private void PrintCommandHelp(string commandName)
        {
            Command command;
            switch (commandName.ToLowerInvariant())
            {
                case "install":
                    command = new InstallCommand();
                    break;

                case "list":
                    command = new ListCommand();
                    break;

                case "find":
                    command = new FindCommand();
                    break;

                default:
                    Console.WriteLine($"Unknown command '{commandName}'.{Environment.NewLine}");
                    return;
            }

            command.Invoke(new string[] { "--help" });
        }
    }
}
