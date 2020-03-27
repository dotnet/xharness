// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Mono.Options;
using System;
using System.Collections.Generic;

namespace XHarness.iOS
{
    /// <summary>
    /// Command which executes a given, already-packaged iOS application, waits on it and returns status based on the outcome.
    /// </summary>
    public class iOSTestCommand : TestCommand
    {
        // Path to packaged app
        string ApplicationPath = "";
        // List of targets to test.
        string[] Targets = Array.Empty<string>();
        // Path where the outputs of execution will be stored.
        string OutputDirectory = "";
        // Path where run logs will hbe stored and projects
        string WorkingDirectory = "";
        // How long XHarness should wait until a test execution completes before clean up (kill running apps, uninstall, etc)
        int TimeoutInSeconds = 300;

        bool ShowHelp = false;

        public iOSTestCommand() : base()
        {
            Options = new OptionSet() {
                "usage: ios test [OPTIONS]",
                "",
                "Packaging command that will create a iOS/tvOS/watchOS or macOS application that can be used to run NUnit or XUnit-based test dlls",
                { "app|a=", "Path to already-packaged app",  v => ApplicationPath = v},
                { "output-directory=|o=", "Directory in which the resulting package will be outputted", v => OutputDirectory = v},
                { "targets=", "Comma-delineated list of targets to test for", v=> Targets = v.Split(',') },
                { "timeout=|t=", "Time span, in seconds, to wait for instrumentation to complete.", v => TimeoutInSeconds = int.Parse(v)},
                { "working-directory=|w=", "Directory in which the resulting package will be outputted", v => WorkingDirectory = v},
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
            Console.WriteLine($"iOS Test command called:{Environment.NewLine}App:{ApplicationPath}{Environment.NewLine}Targets:{string.Join(',', Targets)}");
            Console.WriteLine($"Output Directory:{OutputDirectory}{Environment.NewLine}Working Directory:{WorkingDirectory}{Environment.NewLine}Timeout:{TimeoutInSeconds}s");
            return 0;
        }
    }
}
