// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.CLI.Common;
using Mono.Options;
using System;
using System.Threading.Tasks;

namespace Microsoft.DotNet.XHarness.CLI.iOS
{
    /// <summary>
    /// Command which executes a given, already-packaged iOS application, waits on it and returns status based on the outcome.
    /// </summary>
    internal class iOSTestCommand : TestCommand
    {
        private readonly iOSTestCommandArguments _arguments = new iOSTestCommandArguments();
        protected override ITestCommandArguments TestArguments => _arguments;

        public iOSTestCommand() : base()
        {
            Options = new OptionSet() {
                "usage: ios test [OPTIONS]",
                "",
                "Packaging command that will create a iOS/tvOS/watchOS or macOS application that can be used to run NUnit or XUnit-based test dlls",
                { "app|a=", "Path to already-packaged app",  v => _arguments.AppPackagePath = v},
                { "output-directory=|o=", "Directory in which the resulting package will be outputted", v => _arguments.OutputDirectory = v},
                { "targets=", "Comma-delineated list of targets to test for", v=> _arguments.Targets = v.Split(',') },
                { "timeout=|t=", "Time span, in seconds, to wait for instrumentation to complete.", v => _arguments.Timeout = TimeSpan.FromSeconds(int.Parse(v))},
                { "working-directory=|w=", "Directory in which the resulting package will be outputted", v => _arguments.WorkingDirectory = v},
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

            return Task.FromResult(0);
        }
    }
}
