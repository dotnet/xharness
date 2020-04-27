// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments
{
    internal interface ITestCommandArguments : ICommandArguments
    {
        /// <summary>
        /// Path to packaged app
        /// </summary>
        string AppPackagePath { get; set; }

        /// <summary>
        /// List of targets to test
        /// </summary>
        IReadOnlyCollection<string> Targets { get; set; }

        /// <summary>
        /// How long XHarness should wait until a test execution completes before clean up (kill running apps, uninstall, etc)
        /// </summary>
        TimeSpan Timeout { get; set; }

        /// <summary>
        /// Path where the outputs of execution will be stored
        /// </summary>
        string OutputDirectory { get; set; }
    }

    internal abstract class TestCommandArguments : ITestCommandArguments
    {
        public string AppPackagePath { get; set; }
        public IReadOnlyCollection<string> Targets { get; set; }
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(15);
        public string OutputDirectory { get; set; }
        public LogLevel Verbosity { get; set; }

        public virtual IList<string> GetValidationErrors()
        {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(AppPackagePath))
            {
                errors.Add("You must provide a name for the application that will be tested.");
            }

            if (string.IsNullOrEmpty(OutputDirectory))
            {
                errors.Add("Output directory path missing.");
            }
            else
            {
                if (!Path.IsPathRooted(OutputDirectory))
                {
                    OutputDirectory = Path.Combine(Directory.GetCurrentDirectory(), OutputDirectory);
                }

                if (!Directory.Exists(OutputDirectory))
                {
                    Directory.CreateDirectory(OutputDirectory);
                }
            }

            return errors;
        }
    }
}
