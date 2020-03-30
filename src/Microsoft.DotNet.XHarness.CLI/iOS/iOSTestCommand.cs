// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.CLI.Common;
using Mono.Options;
using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.XHarness.CLI.iOS
{
    /// <summary>
    /// Command which executes a given, already-packaged iOS application, waits on it and returns status based on the outcome.
    /// </summary>
    public class iOSTestCommand : TestCommand
    {
        // Path to packaged app
        private string _applicationPath = "";

        // List of targets to test.
        private string[] _targets = Array.Empty<string>();

        // Path where the outputs of execution will be stored.
        private string _outputDirectory = "";

        // Path where run logs will hbe stored and projects
        private string _workingDirectory = "";

        // How long XHarness should wait until a test execution completes before clean up (kill running apps, uninstall, etc)
        private int _timeoutInSeconds = 300;
        private bool _showHelp = false;

        public iOSTestCommand() : base()
        {
            Options = new OptionSet() {
                "usage: ios test [OPTIONS]",
                "",
                "Packaging command that will create a iOS/tvOS/watchOS or macOS application that can be used to run NUnit or XUnit-based test dlls",
                { "app|a=", "Path to already-packaged app",  v => _applicationPath = v},
                { "output-directory=|o=", "Directory in which the resulting package will be outputted", v => _outputDirectory = v},
                { "targets=", "Comma-delineated list of targets to test for", v=> _targets = v.Split(',') },
                { "timeout=|t=", "Time span, in seconds, to wait for instrumentation to complete.", v => _timeoutInSeconds = int.Parse(v)},
                { "working-directory=|w=", "Directory in which the resulting package will be outputted", v => _workingDirectory = v},
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

            Console.WriteLine($"iOS Test command called:{Environment.NewLine}App:{_applicationPath}{Environment.NewLine}Targets:{string.Join(',', _targets)}");
            Console.WriteLine($"Output Directory:{_outputDirectory}{Environment.NewLine}Working Directory:{_workingDirectory}{Environment.NewLine}Timeout:{_timeoutInSeconds}s");

            return 0;
        }
    }
}
