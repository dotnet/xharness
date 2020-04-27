// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.DotNet.XHarness.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.CLI.Commands.iOS;

namespace Microsoft.DotNet.XHarness.CLI.iOS
{
    internal class iOSPackageCommandArguments : XHarnessCommandArguments
    {
        public string AppPackageName { get; set; }

        public string OutputDirectory { get; set; }

        public string WorkingDirectory { get; set; }

        /// <summary>
        /// A path that is the root of the .ignore files that will be used to skip tests if needed
        /// </summary>
        public string IgnoreFilesRootDirectory { get; set; }

        /// <summary>
        /// A path that is the root of the traits txt files that will be used to skip tests if needed
        /// </summary>
        public string TraitsRootDirectory { get; set; }

        public string MtouchExtraArgs { get; set; }

        public TemplateType SelectedTemplateType { get; set; }

        public override IList<string> GetValidationErrors()
        {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(AppPackageName))
            {
                errors.Add("You must provide a name for the application to be created.");
            }

            if (string.IsNullOrEmpty(OutputDirectory))
            {
                errors.Add("Output directory path missing.");
            }
            else
            {
                OutputDirectory = RootPath(OutputDirectory);
            }

            if (string.IsNullOrEmpty(WorkingDirectory))
            {
                errors.Add("Working directory path missing.");
            }
            else
            {
                WorkingDirectory = RootPath(WorkingDirectory);
            }

            return errors;
        }
    }
}
