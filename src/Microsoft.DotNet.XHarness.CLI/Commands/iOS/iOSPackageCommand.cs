// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.iOS;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.TestImporter.Templates;
using Microsoft.DotNet.XHarness.iOS.Shared.TestImporter.Templates.Managed;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;
using Microsoft.DotNet.XHarness.iOS.TestImporter;
using Microsoft.Extensions.Logging;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.Commands.iOS
{
    /// <summary>
    /// Command that will create the required project generation for the iOS plaform. The command will ensure that all
    /// required .csproj and src are created. The command is part of the parent CommandSet iOS and exposes similar
    /// plus extra options to the one that its Android counterpart exposes.
    /// </summary>
    internal class iOSPackageCommand : XHarnessCommand
    {
        // TODO: Some more parameters we need to make configurable maybe
        //private const string NugetPath = "/Library/Frameworks/Mono.framework/Versions/Current/Commands/nuget";
        //private const string MsBuildPath = "/Library/Frameworks/Mono.framework/Versions/Current/bin/msbuild";
        private const string DotnetPath = "dotnet";
        private readonly TimeSpan _nugetRestoreTimeout = TimeSpan.FromMinutes(10);
        private readonly TimeSpan _msBuildTimeout = TimeSpan.FromMinutes(30);

        private readonly IProcessManager _processManager;

        private readonly iOSPackageCommandArguments _arguments = new iOSPackageCommandArguments();
        protected override ICommandArguments Arguments => _arguments;

        public iOSPackageCommand(IProcessManager processManager = null) : base("package")
        {
            _processManager = processManager ?? new ProcessManager();

            Options = new OptionSet() {
                "usage: ios package [OPTIONS]",
                "",
                "Packaging command that will create a iOS/tvOS/watchOS or macOS application that can be used to run NUnit or XUnit-based test dlls",
                { "name=|n=", "Name of the test application",  v => _arguments.AppPackageName = v},
                { "mtouch-extraargs=|m=", "Extra arguments to be passed to mtouch.", v => _arguments.MtouchExtraArgs = v },
                { "ignore-directory=|i=", "Root directory containing all the *.ignore files used to skip tests if needed.", v => _arguments.IgnoreFilesRootDirectory = v },
                { "template=|t=", "Indicates which template to use. There are two available ones: Managed, which uses Xamarin.[iOS|Mac] and Native (default:Managed).",
                    v => {
                        if (Enum.TryParse(v, out TemplateType template))
                        {
                            _arguments.SelectedTemplateType = template;
                        }
                        else
                        {
                            Console.Error.WriteLine(
                                $"Unknown template type '{v}'. " +
                                $"Allowed values are: {GetAllowedValues<TemplateType>()}");

                            ShowHelp = true;
                        }
                    }
                },
                { "traits-directory=|td=", "Root directory that contains all the .txt files with traits that will be skipped if needed.", v =>  _arguments.TraitsRootDirectory = v },
                { "working-directory=|w=", "Directory that will be used to output generated projects", v => _arguments.WorkingDirectory = v },
                { "output-directory=|o=", "Directory in which the resulting package will be outputted", v => _arguments.OutputDirectory = v},
                { "assembly=|a=", "An assembly to be added as part of the testing application", v => _arguments.Assemblies.Add(v)},
                { "configuration=", "The configuration that will be used to build the app. Default is 'Debug'",
                    v => {
                        if (Enum.TryParse(v, out BuildConfiguration configuration))
                        {
                            _arguments.BuildConfiguration = configuration;
                        }
                        else
                        {
                            Console.Error.WriteLine(
                                $"Unknown build configuration '{v}'. " +
                                $"Allowed values are: {GetAllowedValues<BuildConfiguration>()}");

                            ShowHelp = true;
                        }
                    }
                },
                { "testing-framework=|tf=", "The testing framework that is used by the given assemblies.",
                    v => {
                        if (Enum.TryParse(v, out TestingFramework testingFramework))
                        {
                            _arguments.TestingFramework = testingFramework;
                        }
                        else
                        {
                            Console.Error.WriteLine(
                                $"Unknown build configuration '{v}'. " +
                                $"Allowed values are: {GetAllowedValues<TestingFramework>()}");

                            ShowHelp = true;
                        }
                    }
                },
                { "platform=|p=", "Plaform to be added as the target for the application. Can be used multiple times to target more platforms.",
                    v => { 
                        // split the platforms and try to parse each of them
                        if (Enum.TryParse(v, out Platform platform))
                        {
                            _arguments.Platforms.Add(platform);
                        }
                        else
                        {
                            Console.Error.WriteLine(
                                $"Unknown target platform '{v}'. " +
                                $"Allowed values are: {GetAllowedValues<Platform>()}");

                            ShowHelp = true;
                        }
                    }
                },
                { "help|h", "Show this message", v => ShowHelp = v != null },
            };
        }

        protected async override Task<ExitCode> InvokeInternal()
        {
            // Validate the presence of Xamarin.iOS
            var missingXamariniOS = false;
            if (_arguments.SelectedTemplateType == TemplateType.Managed)
            {
                var dotnetLog = new MemoryLog() { Timestamp = false };
                var process = new Process();
                process.StartInfo.FileName = "bash";
                process.StartInfo.Arguments = "-c \"" + DotnetPath + " --info | grep \\\"Base Path\\\" | cut -d':' -f 2 | tr -d '[:space:]'\"";

                var result = await _processManager.RunAsync(process, new MemoryLog(), dotnetLog, new MemoryLog(), TimeSpan.FromSeconds(5));

                if (result.Succeeded)
                {
                    var sdkPath = dotnetLog.ToString().Trim();
                    if (Directory.Exists(sdkPath))
                    {
                        var xamarinIosPath = Path.Combine(sdkPath, "Xamarin", "iOS", "Xamarin.iOS.CSharp.targets");
                        if (!File.Exists(xamarinIosPath))
                        {
                            missingXamariniOS = true;
                            _log.LogWarning("Failed to find the Xamarin iOS package which is needed for the Managed template: " + xamarinIosPath);
                        }
                    }
                }
            }

            // create the factory, which will be used to find the diff assemblies
            var assemblyLocator = new AssemblyLocator(Directory.GetCurrentDirectory());
            var assemblyDefinitionFactory = new AssemblyDefinitionFactory(_arguments.TestingFramework, assemblyLocator);

            ITemplatedProject template = _arguments.SelectedTemplateType switch
            {
                TemplateType.Managed => new XamariniOSTemplate
                {
                    AssemblyDefinitionFactory = assemblyDefinitionFactory,
                    AssemblyLocator = assemblyDefinitionFactory.AssemblyLocator,
                    OutputDirectoryPath = _arguments.OutputDirectory,
                    IgnoreFilesRootDirectory = _arguments.IgnoreFilesRootDirectory,
                    ProjectFilter = new ProjectFilter(_arguments.IgnoreFilesRootDirectory, _arguments.TraitsRootDirectory),
                },
                _ => throw new Exception("The 'Native' template is not yet supported. Please use the managed one."),
            };

            var logs = new Logs(_arguments.WorkingDirectory);
            var runLog = logs.Create("package-log.txt", "Package Log");
            var consoleLog = new CallbackLog(s => _log.LogInformation(s))
            {
                Timestamp = false
            };

            var aggregatedLog = Log.CreateAggregatedLog(runLog, consoleLog);
            aggregatedLog.WriteLine("Generating scaffold app with:");
            aggregatedLog.WriteLine($"\tAppname: '{_arguments.AppPackageName}'");
            aggregatedLog.WriteLine($"\tAssemblies: '{string.Join(" ", _arguments.Assemblies)}'");
            aggregatedLog.WriteLine($"\tExtraArgs: '{_arguments.MtouchExtraArgs}'");

            // first step, generate the required info to be passed to the factory
            var projects = new List<(string Name, string[] Assemblies, string ExtraArgs, double TimeoutMultiplier)> {
                // TODO: Timeout multiplier handling
                (Name: _arguments.AppPackageName, Assemblies: _arguments.Assemblies.ToArray(), ExtraArgs: _arguments.MtouchExtraArgs, TimeoutMultiplier: 1.0),
            };

            // TODO: we are not taking into account all the plaforms, just iOS
            var allProjects = new GeneratedProjects();
            foreach (var p in _arguments.Platforms)
            {
                // so wish that mono.options allowed use to use async :/
                var testProjects = await template.GenerateTestProjectsAsync(projects, XHarness.iOS.Shared.TestImporter.Platform.iOS);
                allProjects.AddRange(testProjects);
            }

            // We do have all the required projects, time to compile them
            aggregatedLog.WriteLine("Scaffold app generated.");

            // First step, nuget restore whatever is needed
            var projectPath = Path.Combine(_arguments.OutputDirectory, _arguments.AppPackageName + ".csproj");
            aggregatedLog.WriteLine($"Project path is {projectPath}");
            aggregatedLog.WriteLine($"Performing nuget restore.");

            using (var dotnetRestore = new Process())
            {
                dotnetRestore.StartInfo.FileName = DotnetPath;
                var args = new List<string>
                {
                    "restore",
                    projectPath,
                    "--verbosity:detailed"
                };

                dotnetRestore.StartInfo.Arguments = StringUtils.FormatArguments(args);

                var result = await _processManager.RunAsync(dotnetRestore, aggregatedLog, _nugetRestoreTimeout);
                if (result.TimedOut)
                {
                    aggregatedLog.WriteLine("nuget restore timedout.");
                    return ExitCode.GENERAL_FAILURE; // TODO: Make more specific?
                }

                if (!result.Succeeded)
                {
                    aggregatedLog.WriteLine($"nuget restore exited with {result.ExitCode}");
                    return ExitCode.GENERAL_FAILURE; // TODO: Make more specific?
                }
            }

            var finalResult = ExitCode.SUCCESS;

            // perform the build of the application
            using (var dotnetBuild = new Process())
            {
                dotnetBuild.StartInfo.FileName = DotnetPath;

                // TODO: Only taking into account one platform
                if (_arguments.Platforms.Count > 1)
                {
                    _log.LogWarning($"Multi-platform targetting is not supported yet. Targetting {_arguments.Platforms[0]} only.");
                }

                dotnetBuild.StartInfo.Arguments = GetBuildArguments(projectPath, _arguments.Platforms[0].ToString());

                aggregatedLog.WriteLine($"Building {_arguments.AppPackageName} ({projectPath})");

                var result = await _processManager.RunAsync(dotnetBuild, aggregatedLog, _msBuildTimeout);
                if (result.TimedOut)
                {
                    aggregatedLog.WriteLine("Build timed out after {0} seconds.", _msBuildTimeout.TotalSeconds);
                }
                else if (result.Succeeded)
                {
                    aggregatedLog.WriteLine("Build was successful.");
                }
                else if (!result.Succeeded)
                {
                    _log.LogError($"Build failed with return code: {result.ExitCode}");

                    finalResult = ExitCode.GENERAL_FAILURE; // TODO: Make more specific?

                    if (missingXamariniOS)
                    {
                        _log.LogWarning("Possible cause of the failure may be missing Xamarin iOS package");
                    }
                }
            }

            return finalResult;
        }

        public static string GetAllowedValues<T>()
        {
            var names = Enum.GetValues(typeof(T))
                .Cast<T>()
                .Select(t => t.ToString());

            return string.Join(", ", names);
        }

        private string GetBuildArguments(string projectPath, string projectPlatform)
        {
            var binlogPath = Path.Combine(_arguments.WorkingDirectory, "appbuild.binlog");

            return StringUtils.FormatArguments(new[]
            {
                "msbuild",
                "-verbosity:diagnostic",
                $"/bl:{binlogPath}",
                $"/p:Platform={projectPlatform}",
                $"/p:Configuration={_arguments.BuildConfiguration}",
                projectPath
            });
        }
    }
}
