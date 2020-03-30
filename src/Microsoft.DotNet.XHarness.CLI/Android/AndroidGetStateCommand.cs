// Licensed to the .NET Foundation under one or more agreements.0
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Mono.Options;
using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.XHarness.CLI.Android
{
    public class AndroidGetStateCommand : Command
    {
        bool ShowHelp = false;

        public AndroidGetStateCommand() : base("state")
        {
            Options = new OptionSet() {
                "usage: android state",
                "",
                "Print information about the current machine, such as host machine info, path/version of ADB.exe used and device status",
                { "help|h", "Show this message", v => ShowHelp = v != null }
            };
        }

        public override int Invoke(IEnumerable<string> arguments)
        {
            // Deal with unknown options and print nicely 
            var extra = Options.Parse(arguments);
            if (ShowHelp)
            {
                Options.WriteOptionDescriptions(Console.Out);
                return 1;
            }
            if (extra.Count > 0)
            {
                Console.WriteLine($"Unknown arguments: {string.Join(" ", extra)}");
                Options.WriteOptionDescriptions(Console.Out);
                return 2;
            }
            Console.WriteLine("Android state command called (no args supported)");
            return 0;
        }
    }
}
