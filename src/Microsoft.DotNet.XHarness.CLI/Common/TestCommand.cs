// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.Common
{
    internal abstract class TestCommand : XHarnessCommand
    {
        protected override ICommandArguments Arguments => TestArguments;
        protected abstract ITestCommandArguments TestArguments { get; }

        protected readonly OptionSet CommonOptions;

        public TestCommand() : base("test")
        {
            CommonOptions = new OptionSet
            {
                { "app|a=", "Path to already-packaged app", v => TestArguments.AppPackagePath = v },
                { "output-directory=|o=", "Directory in which the resulting package will be outputted", v => TestArguments.OutputDirectory = v},
                { "targets=", "Comma-delineated list of targets to test for", v=> TestArguments.Targets = v.Split(',') },
                { "timeout=|t=", "Time span, in seconds, to wait for instrumentation to complete.", v => TestArguments.Timeout = TimeSpan.FromSeconds(int.Parse(v))},
                { "working-directory=|w=", "Directory in which the resulting package will be outputted", v => TestArguments.WorkingDirectory = v},
                { "help|h", "Show this message", v => ShowHelp = v != null }
            };
        }
    }
}
