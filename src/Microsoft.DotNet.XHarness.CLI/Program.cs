// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.DotNet.XHarness.CLI.Android;
using Microsoft.DotNet.XHarness.CLI.Commands;
using Microsoft.DotNet.XHarness.CLI.Commands.Apple;
using Microsoft.DotNet.XHarness.CLI.Commands.Wasm;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.CLI.Commands;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            Console.WriteLine($"XHarness command issued: {string.Join(' ', args)}");

            // TODO (#400): We can remove this after some time when users get used to the new commands
            if (args.Length > 1 && args[0] == "ios")
            {
                DisplayRenameWarning();
                args[0] = "apple";
            }

            // Root command: will use the platform specific commands to perform the appropriate action.
            var commands = new CommandSet("xharness");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                commands.Add(new AppleCommandSet());
            }
            else if (args[0] == "apple")
            {
                // Otherwise the command would just not be found
                Console.Error.WriteLine("The 'apple' command is not available on non-OSX platforms!");
                return (int)ExitCode.INVALID_ARGUMENTS;
            }

            commands.Add(new AndroidCommandSet());
            commands.Add(new WasmCommandSet());
            commands.Add(new XHarnessHelpCommand());
            commands.Add(new XHarnessVersionCommand());

            // Mono.Options wouldn't allow "--" and CommandSet parser will temporarily rename it
            int result = commands.Run(args.Select(a => a == "--" ? XHarnessCommand.VerbatimArgumentPlaceholder : a));

            string? exitCodeName = null;
            if (result != 0 && Enum.IsDefined(typeof(ExitCode), result))
            {
                exitCodeName = $" ({(ExitCode)result})";
            }

            Console.WriteLine($"XHarness exit code: {result}{exitCodeName}");
            return result;
        }

        // TODO (#400): We can remove this after some time when users get used to the new commands
        public static void DisplayRenameWarning()
        {
            var color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Warning: The 'ios' command has been renamed to 'apple' and will soon be deprecated!");
            Console.ForegroundColor = color;
        }
    }
}
