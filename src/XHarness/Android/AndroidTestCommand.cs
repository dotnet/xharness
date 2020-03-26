// Licensed to the .NET Foundation under one or more agreements.0
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Mono.Options;
using System;
using System.Collections.Generic;

namespace XHarness.Android
{
    public class AndroidTestCommand : TestCommand
    {
        // Path to packaged app
        string ApplicationPath;

        // If specified, attempt to run instrumentation with this name instead of the default for the supplied APK.
        string InstrumentationName;

        // Path where the outputs of execution will be stored.
        string OutputDirectory;

        // Path where run logs will hbe stored and projects
        string WorkingDirectory;

        // How long XHarness should wait until a test execution completes before clean up (kill running apps, uninstall, etc)
        int TimeoutInSeconds = 300;
        readonly Dictionary<string, string> InstrumentationArguments = new Dictionary<string, string>();

        bool ShowHelp = false;

        public AndroidTestCommand() : base()
        {
            Options = new OptionSet() {
                "usage: android test [OPTIONS]",
                "",
                "Executes tests on and Android device, waits up to a given timeout, then copies files off the device.",
                { "app|a=", "Path to .apk file",  v => ApplicationPath = v},
                { "arg", "Argument to pass to the instrumentation, in form key=value", v =>
                    {
                        string[] argPair = v.Split('=');

                        if (argPair.Length != 2)
                        {
                            Options.WriteOptionDescriptions(Console.Out);
                            return;
                        }
                        else
                        {
                            InstrumentationArguments.Add(argPair[0].Trim(), argPair[1].Trim());
                        }
                    }
                },
                { "instrumentation|i=", "If specified, attempt to run instrumentation with this name instead of the default for the supplied APK.",  v => InstrumentationName = v},
                { "output-directory=", "Directory in which test results will be outputted", v => OutputDirectory = v},
                { "targets=", "Unused on Android",  v => { /* Ignore v but don't throw */ } },
                { "timeout=", "Time span, in seconds, to wait for instrumentation to complete.", v => TimeoutInSeconds = int.Parse(v)},
                { "working-directory=", "Directory in which other files (logs, etc) will be outputted", v => WorkingDirectory = v},
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
            Console.WriteLine($"Android Test command called: App = {ApplicationPath}{Environment.NewLine}Instrumentation Name = {InstrumentationName}");
            Console.WriteLine($"Output Directory:{OutputDirectory}{Environment.NewLine}Working Directory = {WorkingDirectory}{Environment.NewLine}Timeout = {TimeoutInSeconds} seconds.");
            return 0;
        }
    }
}
