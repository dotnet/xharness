// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments
{
    internal abstract class TestCommandArguments : XHarnessCommandArguments
    {
        private string? _appPackagePath = null;
        private string? _outputDirectory = null;

        /// <summary>
        /// Path to packaged app
        /// </summary>
        public string AppPackagePath
        {
            get => _appPackagePath ?? throw new ArgumentException("You must provide a path for the app bundle that will be tested.");
            set => _appPackagePath = value;
        }

        // TODO: Create directory
        /// <summary>
        /// Path where the outputs of execution will be stored
        /// </summary>
        public string OutputDirectory
        {
            get => _outputDirectory ?? throw new ArgumentException("You must provide an output directory where results will be stored.");
            set => _outputDirectory = value;
        }

        /// <summary>
        /// List of targets to test
        /// </summary>
        [DisallowNull]
        public virtual IReadOnlyCollection<string> Targets { get; protected set; } = Array.Empty<string>();

        /// <summary>
        /// How long XHarness should wait until a test execution completes before clean up (kill running apps, uninstall, etc)
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(15);

        protected override OptionSet GetOptions()
        {
            var options = new OptionSet
            {
                { "app|a=", "Path to already-packaged app",
                    v => AppPackagePath = RootPath(v)
                },
                { "output-directory=|o=", "Directory in which the resulting package will be outputted",
                    v => OutputDirectory = RootPath(v)
                },
                { "targets=", "Comma-delineated list of targets to test for",
                    v => Targets = v.Split(',')
                },
                { "timeout=|t=", "Time span, in seconds, to wait for instrumentation to complete.",
                    v =>
                    {
                        if (!int.TryParse(v, out var timeout))
                        {
                            throw new ArgumentException("timeout must be an integer - a number of seconds");
                        }

                        Timeout = TimeSpan.FromSeconds(timeout);
                    }
                },
            };

            foreach (var option in base.GetOptions())
            {
                options.Add(option);
            }

            return options;
        }
    }
}
