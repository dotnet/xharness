// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.XHarness.CLI.Commands.iOS;
using Microsoft.DotNet.XHarness.iOS.TestImporter;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.iOS
{
    /// <summary>
    /// Represents the template to be used. Currently only supports the Managed one.
    /// </summary>
    public enum TemplateType
    {
        Unknown,
        Managed,
        Native,
    }

    public enum BuildConfiguration
    {
        Debug,
        Release,
        Release32,
        Release64,
    }

    // TODO: not nice, I'd like to be able to choose tvOS, watchOS, etc
    public enum Platform
    {
        iPhone,
        iPhoneSimulator
    }

    internal class iOSPackageCommandArguments : XHarnessCommandArguments
    {
        /// <summary>
        /// Name of the packaged app
        /// </summary>
        public string AppPackageName { get; set; }

        /// <summary>
        /// Path where the outputs of execution will be stored
        /// </summary>
        public string OutputDirectory { get; set; }

        /// <summary>
        /// Path where run logs will hbe stored and projects
        /// </summary>
        public string WorkingDirectory { get; set; }

        public TemplateType SelectedTemplateType { get; set; } = TemplateType.Managed;

        public BuildConfiguration BuildConfiguration { get; set; } = BuildConfiguration.Debug;

        public List<string> Assemblies { get; } = new List<string>();

        public TestingFramework TestingFramework { get; set; } = TestingFramework.Unknown;

        public List<Platform> Platforms { get; set; } = new List<Platform>();

        public string MtouchExtraArgs { get; set; }

        /// <summary>
        /// A path that is the root of the .ignore files that will be used to skip tests if needed
        /// </summary>
        public string IgnoreFilesRootDirectory { get; set; }

        /// <summary>
        /// A path that is the root of the traits txt files that will be used to skip tests if needed
        /// </summary>
        public string TraitsRootDirectory { get; set; }

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
                WorkingDirectory = RootPath(WorkingDirectory);

                if (!Directory.Exists(WorkingDirectory))
                {
                    Directory.CreateDirectory(WorkingDirectory);
                }
            }

            if (Assemblies.Count == 0)
            {
                errors.Add("No test assemblies provided.");
            }
            else if (!string.IsNullOrEmpty(WorkingDirectory))
            {
                foreach (var a in Assemblies)
                {
                    var assemblyPath = Path.Combine(WorkingDirectory, a);
                    if (!File.Exists(assemblyPath))
                    {
                        errors.Add($"Could not find assembly '{assemblyPath}'");
                    }
                }
            }

            if (Platforms.Count == 0)
            {
                errors.Add($"No platforms provided. Available platforms are: {iOSPackageCommand.GetAllowedValues<Platform>()}");
            }

            if (TestingFramework == TestingFramework.Unknown)
            {
                errors.Add($"Unknown testing framework. Supported frameworks are: {iOSPackageCommand.GetAllowedValues<TestingFramework>()}");
            }

            if (SelectedTemplateType == TemplateType.Native)
            {
                errors.Add("The 'Native' template is not yet supported. Please use the managed one.");
            }
            else if (SelectedTemplateType == TemplateType.Unknown)
            {
                errors.Add($"Please provide a template type. Avaliable templates are: {iOSPackageCommand.GetAllowedValues<TemplateType>()}");
            }

            return errors;
        }
    }
}
