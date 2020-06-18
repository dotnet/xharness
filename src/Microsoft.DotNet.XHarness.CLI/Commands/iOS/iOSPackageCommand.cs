// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.iOS;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.Common.CLI.Commands;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.Common.Utilities;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution.Mlaunch;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.TestImporter.Templates;
using Microsoft.DotNet.XHarness.iOS.Shared.TestImporter.Templates.Managed;
using Microsoft.DotNet.XHarness.iOS.TestImporter;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.iOS
{
    /// <summary>
    /// Command that will create the required project generation for the iOS plaform. The command will ensure that all
    /// required .csproj and src are created. The command is part of the parent CommandSet iOS and exposes similar
    /// plus extra options to the one that its Android counterpart exposes.
    /// </summary>
    internal class iOSPackageCommand : XHarnessCommand
    {
        private const string CommandHelp = "Packaging command that will create a iOS/tvOS/watchOS or macOS application that can be used to run NUnit or XUnit-based test dlls";

        private readonly TimeSpan _nugetRestoreTimeout = TimeSpan.FromMinutes(10);
        private readonly TimeSpan _msBuildTimeout = TimeSpan.FromMinutes(30);

        private readonly iOSPackageCommandArguments _arguments = new iOSPackageCommandArguments();

        protected override string CommandUsage { get; } = "ios package [OPTIONS]";
        protected override string CommandDescription { get; } = CommandHelp;

        protected override XHarnessCommandArguments Arguments => _arguments;

        public iOSPackageCommand() : base("package", allowsExtraArgs: false, CommandHelp)
        {
        }

        protected async override Task<ExitCode> InvokeInternal(ILogger logger)
        {
            var processManager = new MLaunchProcessManager();

            // Validate the presence of Xamarin.iOS
            var missingXamariniOS = false;
            if (_arguments.TemplateType == TemplateType.Managed)
            {
                var dotnetLog = new MemoryLog() { Timestamp = false };
                var process = new Process();
                process.StartInfo.FileName = "bash";
                process.StartInfo.Arguments = "-c \"" + _arguments.DotnetPath + " --info | grep \\\"Base Path\\\" | cut -d':' -f 2 | tr -d '[:space:]'\"";

                var result = await processManager.RunAsync(process, new NullLog(), dotnetLog, new NullLog(), TimeSpan.FromSeconds(5));

                if (result.Succeeded)
                {
                    var sdkPath = dotnetLog.ToString().Trim();
                    if (Directory.Exists(sdkPath))
                    {
                        var xamarinIosPath = Path.Combine(sdkPath, "Xamarin", "iOS", "Xamarin.iOS.CSharp.targets");
                        if (!File.Exists(xamarinIosPath))
                        {
                            missingXamariniOS = true;
                            logger.LogWarning("Failed to find the Xamarin iOS package which is needed for the Managed template: " + xamarinIosPath);
                        }
                    }
                }
            }

            // create the factory, which will be used to find the diff assemblies
            var assemblyLocator = new AssemblyLocator(Directory.GetCurrentDirectory());
            var assemblyDefinitionFactory = new AssemblyDefinitionFactory(_arguments.TestingFramework, assemblyLocator);

            ITemplatedProject template = _arguments.TemplateType switch
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
            var consoleLog = new CallbackLog(s => logger.LogInformation(s))
            {
                Timestamp = false
            };

            var aggregatedLog = Log.CreateReadableAggregatedLog(runLog, consoleLog);
            aggregatedLog.WriteLine("Generating scaffold app with:");
            aggregatedLog.WriteLine($"\tAppname: '{_arguments.AppPackageName}'");
            aggregatedLog.WriteLine($"\tAssemblies: '{string.Join(" ", _arguments.Assemblies)}'");
            aggregatedLog.WriteLine($"\tExtraArgs: '{_arguments.MtouchExtraArgs}'");

            // first step, generate the required info to be passed to the factory
            var projects = new List<(string Name, string[] Assemblies, string? ExtraArgs, double TimeoutMultiplier)> {
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
                dotnetRestore.StartInfo.FileName = _arguments.DotnetPath;
                var args = new List<string>
                {
                    "restore",
                    projectPath,
                    "--verbosity:detailed"
                };

                dotnetRestore.StartInfo.Arguments = StringUtils.FormatArguments(args);

                var result = await processManager.RunAsync(dotnetRestore, aggregatedLog, _nugetRestoreTimeout);
                if (result.TimedOut)
                {
                    aggregatedLog.WriteLine("nuget restore timedout.");
                    return ExitCode.PACKAGE_BUNDLING_FAILURE_NUGET_RESTORE;
                }

                if (!result.Succeeded)
                {
                    aggregatedLog.WriteLine($"nuget restore exited with {result.ExitCode}");
                    return ExitCode.PACKAGE_BUNDLING_FAILURE_NUGET_RESTORE;
                }
            }

            var finalResult = ExitCode.SUCCESS;

            // perform the build of the application
            using (var dotnetBuild = new Process())
            {
                dotnetBuild.StartInfo.FileName = _arguments.DotnetPath;

                // TODO: Only taking into account one platform
                // https://github.com/dotnet/xharness/issues/105
                if (_arguments.Platforms.Count > 1)
                {
                    logger.LogWarning($"Multi-platform targetting is not supported yet. Targetting {_arguments.Platforms[0]} only.");
                }

                dotnetBuild.StartInfo.Arguments = GetBuildArguments(projectPath, _arguments.Platforms[0].ToString());

                aggregatedLog.WriteLine($"Building {_arguments.AppPackageName} ({projectPath})");

                var result = await processManager.RunAsync(dotnetBuild, aggregatedLog, _msBuildTimeout);
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
                    logger.LogError($"Build failed with return code: {result.ExitCode}");

                    finalResult = ExitCode.PACKAGE_BUNDLING_FAILURE_BUILD;

                    if (missingXamariniOS)
                    {
                        logger.LogWarning("Possible cause of the failure may be missing Xamarin iOS package");
                    }
                }
            }

            return finalResult;
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
