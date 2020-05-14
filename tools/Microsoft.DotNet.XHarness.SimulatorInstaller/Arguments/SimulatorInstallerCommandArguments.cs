// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.SimulatorInstaller.Arguments
{
    internal abstract class SimulatorInstallerCommandArguments : XHarnessCommandArguments
    {
        private string? _xcodeRoot;

        /// <summary>
        /// Path to where Xcode is located.
        /// </summary>
        public string XcodeRoot
        {
            get
            {
                if (_xcodeRoot == null)
                {
                    // Determine it automatically from xcode-select
                    _xcodeRoot = new MacOSProcessManager().XcodeRoot;
                }

                return _xcodeRoot;
            }
            set => _xcodeRoot = value;
        }

        protected sealed override OptionSet GetCommandOptions()
        {
            var options = GetAdditionalOptions();

            options.Add("xcode=", "Path to where Xcode is located, e.g. /Application/Xcode114.app. If not set, xcode-select is used to determine the location", v => XcodeRoot = RootPath(v));

            return options;
        }

        protected abstract OptionSet GetAdditionalOptions();

        public override void Validate()
        {
            if (!Directory.Exists(XcodeRoot))
            {
                throw new ArgumentException("Invalid Xcode path supplied");
            }

            var plistPath = Path.Combine(XcodeRoot, "Contents", "Info.plist");
            if (!File.Exists(plistPath))
            {
                throw new ArgumentException($"Cannot find Xcode. The path '{plistPath}' does not exist.");
            }
        }
    }
}
