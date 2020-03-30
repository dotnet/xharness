// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.CLI.Android;
using Microsoft.DotNet.XHarness.CLI.iOS;
using Mono.Options;
using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.XHarness.CLI.Common
{
    class XHarnessHelpCommand : HelpCommand
    {
        public override int Invoke(IEnumerable<string> arguments)
        {
            string[] args = new List<string>(arguments).ToArray();

            if (args.Length == 0)
            {
                return base.Invoke(arguments);
            }

            string subCommand = "";
            if (args.Length >= 2)
            {
                subCommand = args[1];
            }

            switch (args[0].ToLowerInvariant())
            {
                case "android":
                    Console.WriteLine("All supported Android commands: (run 'XHarness android {command} --help' for more details)");
                    PrintAllHelp(new AndroidCommandSet(), subCommand);
                    break;
                case "ios":
                    Console.WriteLine("All supported iOS commands: (run 'XHarness ios {command} --help' for more details)");
                    PrintAllHelp(new iOSCommandSet(), subCommand);
                    break;
                default:
                    Console.WriteLine($"No help available for command '{args[0]}'");
                    return -1;
            }
            return 0;

        }

        private void PrintAllHelp(CommandSet commandSet, string v)
        {
            HelpCommand helpCommand = new HelpCommand();
            commandSet.Add(helpCommand);
            commandSet.Run(new string[] { "help" });
        }
    }
}
