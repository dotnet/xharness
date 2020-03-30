// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.XHarness.CLI.Common
{
    internal interface IPackageCommandArguments : ICommandArguments
    {
        /// <summary>
        /// Name of the packaged app
        /// </summary>
        string AppPackageName { get; set; }

        /// <summary>
        /// Path where the outputs of execution will be stored
        /// </summary>
        string OutputDirectory { get; set; }

        /// <summary>
        /// Path where run logs will hbe stored and projects
        /// </summary>
        string WorkingDirectory { get; set; }
    }

    internal abstract class PackageCommandArguments : IPackageCommandArguments
    {
        public string AppPackageName { get; set; }
        public string OutputDirectory { get; set; }
        public string WorkingDirectory { get; set; }

        public virtual bool TryValidate([NotNullWhen(true)] out IEnumerable<string> errors)
        {
            var errs = new List<string>();
            errors = errs;

            if (string.IsNullOrEmpty(AppPackageName))
            {
                errs.Add("You must provide a name for the application to be created.");
            }

            if (string.IsNullOrEmpty(OutputDirectory))
            {
                errs.Add("Output directory path missing.");
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
                errs.Add("Working directory path missing.");
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

            return !errors.Any();
        }
    }
}
