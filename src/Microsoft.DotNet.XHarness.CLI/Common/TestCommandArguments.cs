// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Common
{
    internal abstract class TestCommandArguments : ITestCommandArguments
    {
        public string AppPackagePath { get; set; }
        public IReadOnlyCollection<string> Targets { get; set; }
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(15);
        public string OutputDirectory { get; set; }
        public string WorkingDirectory { get; set; }
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

            if (string.IsNullOrEmpty(WorkingDirectory))
            {
                errors.Add("Working directory path missing.");
            }
            else
            {
                if (!Path.IsPathRooted(WorkingDirectory))
                {
                    WorkingDirectory = Path.Combine(Directory.GetCurrentDirectory(), WorkingDirectory);
                }

                if (!Directory.Exists(WorkingDirectory))
                {
                    Directory.CreateDirectory(WorkingDirectory);
                }
            }

            return errors;
        }
    }
}
