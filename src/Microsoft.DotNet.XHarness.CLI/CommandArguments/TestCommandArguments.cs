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
    internal abstract class TestCommandArguments : XHarnessCommandArguments
    {
        private readonly List<string> _singleMethodFilters = new List<string>();
        private readonly List<string> _classMethodFilters = new List<string>();

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
        public virtual IReadOnlyCollection<string> Targets { get; protected set; } = Array.Empty<string>();

        /// <summary>
        /// How long XHarness should wait until a test execution completes before clean up (kill running apps, uninstall, etc)
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Methods to be included in the test run while all others are ignored.
        /// </summary>
        public IEnumerable<string> SingleMethodFilters => _singleMethodFilters;

        /// <summary>
        /// Tests classes to be included in the run while all others are ignored.
        /// </summary>
        public IEnumerable<string> ClassMethodFilters => _classMethodFilters;

        protected sealed override OptionSet GetCommandOptions()
        {
            var options = new OptionSet
            {
                { "app|a=", "Path to already-packaged app",
                    v => AppPackagePath = RootPath(v)
                },
                { "output-directory=|o=", "Directory in which the resulting package will be outputted",
                    v => OutputDirectory = RootPath(v)
                },
                { "targets=|t=", "Comma-delineated list of targets to test for",
                    v => Targets = v.Split(',')
                },
                { "timeout=", "Time span, in seconds, to wait for instrumentation to complete.",
                    v =>
                    {
                        if (!int.TryParse(v, out var timeout))
                        {
                            throw new ArgumentException("timeout must be an integer - a number of seconds");
                        }

                        Timeout = TimeSpan.FromSeconds(timeout);
                    }
                },
                {
                    "method|m=", "Method to be ran in the test application. When this parameter is used only the " +
                    "tests that have been provided by the '--method' and '--class' arguments will be ran. All other test will be " +
                    "ignored. Can be used more than once.",
                    v => _singleMethodFilters.Add(v)
                },
                {
                    "class|c=", "Test class to be ran in the test application. When this parameter is used only the " +
                    "tests that have been provided by the '--method' and '--class' arguments will be ran. All other test will be " +
                    "ignored. Can be used more than once.",
                    v => _classMethodFilters.Add(v)
                }
            };

            foreach (var option in GetTestCommandOptions())
            {
                options.Add(option);
            }

            return options;
        }

        protected abstract OptionSet GetTestCommandOptions();

        public override void Validate()
        {
            if (!Directory.Exists(OutputDirectory))
            {
                Directory.CreateDirectory(OutputDirectory);
            }
        }
    }
}
