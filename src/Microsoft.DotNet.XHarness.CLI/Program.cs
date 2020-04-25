// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.DotNet.XHarness.CLI.Android;
using Microsoft.DotNet.XHarness.CLI.Commands;
using Microsoft.DotNet.XHarness.CLI.Commands.iOS;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI
{
    public static class Program
    {


        public static int Main(string[] args)
        {
            Console.WriteLine($"XHarness command issued: {string.Join(' ', args)}");

            // Root command: will use the platform specific commands to perform the appropriate action.
            var commands = new CommandSet("xharness")
            {
                // Add per-platform CommandSets - If adding  a new supported set, that goes here. 
                new iOSCommandSet(),
                new AndroidCommandSet(),

                // add shared commands, for example, help and so on. --version, --help, --verbosity and so on
                new XHarnessHelpCommand()
            };

            int result = commands.Run(args);
            Console.WriteLine($"XHarness exit code: {result}");
            return result;
        }
    }
}
