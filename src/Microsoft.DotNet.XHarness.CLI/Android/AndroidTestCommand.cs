// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.CLI.Common;
using Mono.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.DotNet.XHarness.CLI.Android
{
    internal class AndroidTestCommand : TestCommand
    {
        private readonly AndroidTestCommandArguments _arguments = new AndroidTestCommandArguments();
        protected override ITestCommandArguments TestArguments => _arguments;

        public AndroidTestCommand() : base()
        {
            Options = new OptionSet() {
                "usage: android test [OPTIONS]",
                "",
                "Executes tests on and Android device, waits up to a given timeout, then copies files off the device.",
                { "app|a=", "Path to already-packaged app",  v => _arguments.AppPackagePath = v},
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
                            _arguments.InstrumentationArguments.Add(argPair[0].Trim(), argPair[1].Trim());
                        }
                    }
                },
                { "instrumentation|i=", "If specified, attempt to run instrumentation with this name instead of the default for the supplied APK.",  v => _arguments.InstrumentationName = v},
                { "output-directory=", "Directory in which test results will be outputted", v => _arguments.OutputDirectory = v},
                { "targets=", "Unused on Android",  v => { /* Ignore v but don't throw */ } },
                { "timeout=", "Time span, in seconds, to wait for instrumentation to complete.", v => _arguments.Timeout = TimeSpan.FromSeconds(int.Parse(v)) },
                { "working-directory=", "Directory in which other files (logs, etc) will be outputted", v => _arguments.WorkingDirectory = v},
                { "help|h", "Show this message", v => ShowHelp = v != null }
            };
        }

        protected override Task<int> InvokeInternal()
        {
            Console.WriteLine($"iOS Test command called:");
            Console.WriteLine($"  App: {_arguments.AppPackagePath}");
            Console.WriteLine($"  Targets: {string.Join(',', _arguments.Targets)}");
            Console.WriteLine($"  Output Directory: {_arguments.OutputDirectory}");
            Console.WriteLine($"  Working Directory: {_arguments.WorkingDirectory}");
            Console.WriteLine($"  Timeout: {_arguments.Timeout.TotalSeconds}s");

            Console.WriteLine("Arguments to instrumentation:");

            foreach (KeyValuePair<string, string> pair in _arguments.InstrumentationArguments)
            {
                Console.WriteLine($"  {pair.Key} = {pair.Value}");
            }

            return Task.FromResult(0);
        }
    }
}
