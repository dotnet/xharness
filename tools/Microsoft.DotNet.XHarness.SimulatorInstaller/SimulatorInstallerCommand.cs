// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.DotNet.XHarness.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.CLI.Commands;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.SimulatorInstaller
{
    internal abstract class SimulatorInstallerCommandArguments : XHarnessCommandArguments
    {
        /// <summary>
        /// Path to where Xcode is located.
        /// </summary>
        public string? XcodeRoot { get; set; }

        /// <summary>
        /// How long XHarness should wait until a test execution completes before clean up (kill running apps, uninstall, etc)
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(15);

        protected sealed override OptionSet GetCommandOptions()
        {
            var options = GetAdditionalOptions();

            options.Add("xcode=", "Path where Xcode is installed", v => XcodeRoot = RootPath(v));

            return options;
        }

        protected abstract OptionSet GetAdditionalOptions();

        public override void Validate()
        {
            if (XcodeRoot != null && !Directory.Exists(XcodeRoot))
            {
                throw new ArgumentException("Invalid Xcode path supplied");
            }
        }
    }


    internal abstract class SimulatorInstallerCommand : XHarnessCommand
    {
        protected override XHarnessCommandArguments Arguments => SimulatorInstallerArguments;

        protected abstract SimulatorInstallerCommandArguments SimulatorInstallerArguments { get; }

        protected SimulatorInstallerCommand(string name) : base(name)
        {
        }
    }
}
