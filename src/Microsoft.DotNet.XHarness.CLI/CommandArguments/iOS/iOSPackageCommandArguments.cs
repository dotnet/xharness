// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.XHarness.iOS.TestImporter;
using Mono.Options;

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

    // TODO: Support other platforms such as tvOS, watchOS, etc
    //       https://github.com/dotnet/xharness/issues/105
    public enum Platform
    {
        iPhone,
        iPhoneSimulator
    }

    internal class iOSPackageCommandArguments : XHarnessCommandArguments
    {
        private string? _appPackageName = null;
        private string? _outputDirectory = null;
        private string? _workingDirectory = null;

        /// <summary>
        /// Name of the packaged app
        /// </summary>
        public string AppPackageName
        {
            get => _appPackageName ?? throw new ArgumentException("You must provide a name for the app bundle that will be created.");
            set => _appPackageName = value;
        }

        /// <summary>
        /// Path where the outputs of execution will be stored
        /// </summary>
        public string OutputDirectory
        {
            get => _outputDirectory ?? throw new ArgumentException("You must provide an output directory where results will be stored.");
            set => _outputDirectory = RootPath(value);
        }

        /// <summary>
        /// Path where the assemblies used that will be included in the bundle are stored
        /// </summary>
        public string WorkingDirectory
        {
            get => _workingDirectory ?? throw new ArgumentException("You must provide an output directory where results will be stored.");
            set => _workingDirectory = RootPath(value);
        }

        public List<string> Assemblies { get; } = new List<string>();

        public TemplateType TemplateType { get; set; } = TemplateType.Managed;

        public BuildConfiguration BuildConfiguration { get; set; } = BuildConfiguration.Debug;

        public TestingFramework TestingFramework { get; set; } = TestingFramework.Unknown;

        public List<Platform> Platforms { get; set; } = new List<Platform>();

        public string? MtouchExtraArgs { get; set; }

        /// <summary>
        /// A path that is the root of the .ignore files that will be used to skip tests if needed
        /// </summary>
        public string? IgnoreFilesRootDirectory { get; set; }

        /// <summary>
        /// A path that is the root of the traits txt files that will be used to skip tests if needed
        /// </summary>
        public string? TraitsRootDirectory { get; set; }

        /// <summary>
        /// Path to the 'dotnet' command
        /// </summary>
        public string DotnetPath { get; set; } = "dotnet";

        protected override OptionSet GetCommandOptions() => new OptionSet
        {
            { "name=|n=", "Name of the test application",
                v => AppPackageName = v
            },
            { "mtouch-extraargs=|m=", "Extra arguments to be passed to mtouch.",
                v => MtouchExtraArgs = v
            },
            { "ignore-directory=|i=", "Root directory containing all the *.ignore files used to skip tests if needed.",
                v => IgnoreFilesRootDirectory = RootPath(v)
            },
            { "template=|t=", "Indicates which template to use. There are two available ones: Managed, which uses Xamarin.[iOS|Mac] and Native (default:Managed).",
                v => TemplateType = ParseArgument("template", v, invalidValues: TemplateType.Unknown)
            },
            { "traits-directory=|td=", "Root directory that contains all the .txt files with traits that will be skipped if needed.",
                v =>  TraitsRootDirectory = RootPath(v)
            },
            { "working-directory=|w=", "Directory that will be used to output generated projects",
                v => WorkingDirectory = RootPath(v)
            },
            { "output-directory=|o=", "Directory in which the resulting package will be outputted",
                v => OutputDirectory = RootPath(v)
            },
            { "assembly=|a=", "An assembly to be added as part of the testing application",
                v => Assemblies.Add(v)
            },
            { "configuration=", "The configuration that will be used to build the app. Default is 'Debug'",
                v => BuildConfiguration = ParseArgument<BuildConfiguration>("configuration", v)
            },
            { "testing-framework=|tf=", "The testing framework that is used by the given assemblies.",
                v => TestingFramework = ParseArgument("testing framework", v, invalidValues: TestingFramework.Unknown)
            },
            { "platform=|p=", "Plaform to be added as the target for the application. Can be used multiple times to target more platforms.",
                v => Platforms.Add(ParseArgument<Platform>("platform", v))
            },
            { "dotnet=", "Path to the 'dotnet' command. Default is 'dotnet'",
                v => DotnetPath = v
            },
        };

        public override void Validate()
        {
            // This validates required fields
            AppPackageName = AppPackageName;
            TestingFramework = TestingFramework;

            if (!Directory.Exists(OutputDirectory))
            {
                Directory.CreateDirectory(OutputDirectory);
            }

            if (!Directory.Exists(WorkingDirectory))
            {
                Directory.CreateDirectory(WorkingDirectory);
            }

            if (Assemblies.Count == 0)
            {
                throw new ArgumentException("No test assemblies provided.");
            }
            else if (!string.IsNullOrEmpty(WorkingDirectory))
            {
                foreach (var a in Assemblies)
                {
                    var assemblyPath = Path.Combine(WorkingDirectory, a);
                    if (!File.Exists(assemblyPath))
                    {
                        throw new ArgumentException($"Could not find assembly '{assemblyPath}'");
                    }
                }
            }

            if (Platforms.Count == 0)
            {
                throw new ArgumentException($"No platforms provided. Available platforms are: {GetAllowedValues<Platform>()}");
            }

            if (TemplateType == TemplateType.Native)
            {
                throw new ArgumentException("The 'Native' template is not yet supported. Please use the managed one.");
            }
            else if (TemplateType == TemplateType.Unknown)
            {
                throw new ArgumentException($"Please provide a template type. Avaliable templates are: {GetAllowedValues<TemplateType>()}");
            }
        }
    }
}
