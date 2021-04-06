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
            bool shouldOutput = !IsOutputSensitive(args);

            if (shouldOutput)
            {
                Console.WriteLine($"XHarness command issued: {string.Join(' ', args)}");
            }

            if (args.Length > 0)
            {
                // TODO (#502): We can remove this after some time when users get used to the new commands
                if (args[0] == "ios")
                {
                    DisplayRenameWarning();
                    args[0] = "apple";
                }

#if !DEBUG
                if (args[0] == "apple" && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    // Otherwise the command would just not be found
                    Console.Error.WriteLine("The 'apple' command is not available on non-OSX platforms!");
                    return (int)ExitCode.INVALID_ARGUMENTS;
                }
#endif

                // Mono.Options wouldn't allow "--" so we will temporarily rename it and parse it ourselves later
                args = args.Select(a => a == "--" ? XHarnessCommand.VerbatimArgumentPlaceholder : a).ToArray();
            }

            var commands = GetXHarnessCommandSet();
            int result = commands.Run(args);

            string? exitCodeName = null;
            if (args.Length > 0 && result != 0 && Enum.IsDefined(typeof(ExitCode), result))
            {
                exitCodeName = $" ({(ExitCode)result})";
            }

            if (shouldOutput)
            {
                Console.WriteLine($"XHarness exit code: {result}{exitCodeName}");
            }

            return result;
        }

        // TODO (#502): We can remove this after some time when users get used to the new commands
        public static void DisplayRenameWarning()
        {
            var color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Warning: The 'ios' command has been renamed to 'apple' and will soon be deprecated!");
            Console.ForegroundColor = color;
        }

        public static CommandSet GetXHarnessCommandSet()
        {
            var commandSet = new CommandSet("xharness");

#if !DEBUG
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                commandSet.Add(new AppleCommandSet());
            }
#else
            commandSet.Add(new AppleCommandSet());
#endif

            commandSet.Add(new AndroidCommandSet());
            commandSet.Add(new WasmCommandSet());
            commandSet.Add(new XHarnessHelpCommand());
            commandSet.Add(new XHarnessVersionCommand());

            return commandSet;
        }

        /// <summary>
        /// Returns true when the command outputs data suitable for parsing and we should keep the output clean.
        /// </summary>
        private static bool IsOutputSensitive(string[] args)
        {
            if (args.Length < 2 || args.Contains("--help") || args.Contains("-h"))
            {
                return false;
            }

            switch (args[0])
            {
                case "ios":
                case "apple":
                case "android":
                    return args[1] == "device";
            }

            return false;
        }
    }
}
