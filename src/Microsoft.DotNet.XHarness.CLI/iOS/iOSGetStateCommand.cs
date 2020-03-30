// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Mono.Options;
using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.XHarness.CLI.iOS
{
    public class iOSGetStateCommand : Command
    {
        private bool _showHelp = false;

        public iOSGetStateCommand() : base("state")
        {
            Options = new OptionSet() {
                "usage: ios state",
                "",
                "Print information about the current machine, such as host machine info and device status",
                { "help|h", "Show this message", v => _showHelp = v != null }
            };
        }

        public override int Invoke(IEnumerable<string> arguments)
        {
            // Deal with unknown options and print nicely 
            var extra = Options.Parse(arguments);
            if (_showHelp)
            {
                Options.WriteOptionDescriptions(Console.Out);
                return 1;
            }

            if (extra.Count > 0)
            {
                Console.WriteLine($"Unknown arguments: {string.Join(" ", extra)}");
                Options.WriteOptionDescriptions(Console.Out);
            }

            Console.WriteLine("iOS state command called (no args supported)");

            return 0;
        }
    }
}
