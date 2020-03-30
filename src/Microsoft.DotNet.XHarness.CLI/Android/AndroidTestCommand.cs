// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.CLI.Common;
using Mono.Options;
using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.XHarness.CLI.Android
{
    public class AndroidTestCommand : TestCommand
    {
        // Path to packaged app
        private string _applicationPath;

        // If specified, attempt to run instrumentation with this name instead of the default for the supplied APK.
        private string _instrumentationName;

        // Path where the outputs of execution will be stored.
        private string _outputDirectory;

        // Path where run logs will hbe stored and projects
        private string _workingDirectory;

        // How long XHarness should wait until a test execution completes before clean up (kill running apps, uninstall, etc)
        private int _timeoutInSeconds = 300;
        private readonly Dictionary<string, string> _instrumentationArguments = new Dictionary<string, string>();
        private bool _showHelp = false;

        public AndroidTestCommand() : base()
        {
            Options = new OptionSet() {
                "usage: android test [OPTIONS]",
                "",
                "Executes tests on and Android device, waits up to a given timeout, then copies files off the device.",
                { "app|a=", "Path to .apk file",  v => _applicationPath = v},
                { "arg=", "Argument to pass to the instrumentation, in form key=value", v =>
                    {
                        string[] argPair = v.Split('=');

                        if (argPair.Length != 2)
                        {
                            Options.WriteOptionDescriptions(Console.Out);
                            return;
                        }
                        else
                        {
                            _instrumentationArguments.Add(argPair[0].Trim(), argPair[1].Trim());
                        }
                    }
                },
                { "instrumentation|i=", "If specified, attempt to run instrumentation with this name instead of the default for the supplied APK.",  v => _instrumentationName = v},
                { "output-directory=", "Directory in which test results will be outputted", v => _outputDirectory = v},
                { "targets=", "Unused on Android",  v => { /* Ignore v but don't throw */ } },
                { "timeout=", "Time span, in seconds, to wait for instrumentation to complete.", v => _timeoutInSeconds = int.Parse(v)},
                { "working-directory=", "Directory in which other files (logs, etc) will be outputted", v => _workingDirectory = v},
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
                return 2;
            }

            Console.WriteLine($"Android Test command called: App = {_applicationPath}{Environment.NewLine}Instrumentation Name = {_instrumentationName}");
            Console.WriteLine($"Output Directory:{_outputDirectory}{Environment.NewLine}Working Directory = {_workingDirectory}{Environment.NewLine}Timeout = {_timeoutInSeconds} seconds.");
            Console.WriteLine("Arguments to instrumentation:");

            foreach (var key in _instrumentationArguments.Keys)
            {
                Console.WriteLine($"  {key} = {_instrumentationArguments[key]}");
            }

            return 0;
        }
    }
}
