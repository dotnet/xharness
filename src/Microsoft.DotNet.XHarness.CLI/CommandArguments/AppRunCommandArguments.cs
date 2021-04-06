// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments
{
    internal abstract class AppRunCommandArguments : XHarnessCommandArguments
    {
        private string? _appPackagePath = null;
        private string? _outputDirectory = null;

        /// <summary>
        /// Path to packaged app
        /// </summary>
        public string AppPackagePath
        {
            get => _appPackagePath ?? throw new ArgumentException("You must provide a path for the app that will be tested.");
            set => _appPackagePath = value;
        }

        /// <summary>
        /// Path where the outputs of execution will be stored
        /// </summary>
        public string OutputDirectory
        {
            get => _outputDirectory ?? throw new ArgumentException("You must provide an output directory where results will be stored.");
            set => _outputDirectory = value;
        }

        /// <summary>
        /// List of targets where the app will be run
        /// </summary>
        public virtual IReadOnlyCollection<string> Targets { get; protected set; } = Array.Empty<string>();

        /// <summary>
        /// How long XHarness should wait until a test execution completes before clean up (kill running apps, uninstall, etc)
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(15);

        protected override OptionSet GetCommandOptions() => new()
        {
            {
                "app|a=", "Path to already-packaged app",
                v => AppPackagePath = RootPath(v)
            },
            {
                "output-directory=|o=", "Directory in which the resulting package will be outputted",
                v => OutputDirectory = RootPath(v)
            },
            {
                "targets=|t=", "Comma-delimited list of targets to test for",
                v => Targets = v.Split(',')
            },
            {
                "timeout=", "TimeSpan or number of seconds to wait for instrumentation to complete",
                v =>
                {
                    if (int.TryParse(v, out var timeout))
                    {
                        Timeout = TimeSpan.FromSeconds(timeout);
                        return;
                    }

                    if (TimeSpan.TryParse(v, out var timespan))
                    {
                        Timeout = timespan;
                        return;
                    }

                    throw new ArgumentException("timeout must be an integer - a number of seconds, or a timespan (00:30:00)");
                }
            },
        };

        public override void Validate()
        {
            if (!Directory.Exists(OutputDirectory))
            {
                Directory.CreateDirectory(OutputDirectory);
            }
        }
    }
}
