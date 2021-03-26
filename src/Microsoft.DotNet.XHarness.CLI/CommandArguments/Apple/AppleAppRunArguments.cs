// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple
{
    internal abstract class AppleAppRunArguments : AppRunCommandArguments
    {
        /// <summary>
        /// Path to where Xcode is located.
        /// </summary>
        public string? XcodeRoot { get; set; }

        /// <summary>
        /// Path to the mlaunch binary.
        /// Default comes from the NuGet.
        /// </summary>
        public string MlaunchPath { get; set; }

        /// <summary>
        /// Name of a specific device we want to target.
        /// </summary>
        public string? DeviceName { get; set; }

        /// <summary>
        /// Enable the lldb debugger to be used with the launched application.
        /// </summary>
        public bool EnableLldb { get; set; }

        /// <summary>
        /// Environmental variables set when executing the application.
        /// </summary>
        public IReadOnlyCollection<(string, string)> EnvironmentalVariables => _environmentalVariables;
        private readonly List<(string, string)> _environmentalVariables = new();

        /// <summary>
        /// Kills running simulator processes and removes any previous data before running.
        /// </summary>
        public bool ResetSimulator { get; set; }

        public override IReadOnlyCollection<string> Targets
        {
            get => RunTargets.Select(t => t.AsString()).ToArray();
            protected set
            {
                var testTargets = new List<TestTargetOs>();

                foreach (var targetName in value ?? throw new ArgumentException("Targets cannot be empty"))
                {
                    try
                    {
                        testTargets.Add(targetName.ParseAsAppRunnerTargetOs());
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        throw new ArgumentException(
                            $"Failed to parse test target '{targetName}'. Available targets are:" +
                            GetAllowedValues(t => t.AsString(), invalidValues: TestTarget.None) +
                            Environment.NewLine + Environment.NewLine +
                            "You can also specify desired iOS/tvOS/WatchOS version. Example: ios-simulator-64_13.4");
                    }
                }

                RunTargets = testTargets;
            }
        }

        /// <summary>
        /// Parsed strong-typed targets.
        /// </summary>
        public IReadOnlyCollection<TestTargetOs> RunTargets { get; private set; } = Array.Empty<TestTargetOs>();

        protected AppleAppRunArguments()
        {
            string? pathFromEnv = Environment.GetEnvironmentVariable(Common.CLI.EnvironmentVariables.Names.MLAUNCH_PATH);
            if (!string.IsNullOrEmpty(pathFromEnv))
            {
                MlaunchPath = pathFromEnv;
            }
            else
            {
                // This path is where mlaunch is when the .NET tool is extracted
                var assemblyPath = Path.GetDirectoryName(System.Reflection.Assembly.GetAssembly(typeof(AppleTestCommandArguments))?.Location);
                MlaunchPath = Path.Join(assemblyPath, "..", "..", "..", "runtimes", "any", "native", "mlaunch", "bin", "mlaunch");
            }
        }

        protected override OptionSet GetCommandOptions()
        {
            var options = base.GetCommandOptions();

            var runOptions = new OptionSet
            {
                {
                    "xcode=", "Path where Xcode is installed",
                    v => XcodeRoot = RootPath(v)
                },
                {
                    "mlaunch=", "Path to the mlaunch binary",
                    v => MlaunchPath = RootPath(v)
                },
                {
                    "device-name=", "Name of a specific device, if you wish to target one",
                    v => DeviceName = v
                },
                {
                    "enable-lldb", "Allow to debug the launched application using lldb",
                    v => EnableLldb = v != null
                },
                {
                    "reset-simulator", "Shuts down the simulator and clears all data before running. Shuts it down after the run too",
                    v => ResetSimulator = v != null
                },
                {
                    "set-env=", "Environmental variable to set for the application in format key=value. Can be used multiple times",
                    v => {
                        var position = v.IndexOf('=');
                        if (position == -1)
                        {
                            throw new ArgumentException($"The set-env argument {v} must be in the key=value format");
                        }

                        var key = v.Substring(0, position);
                        var value = v.Substring(position + 1);
                        _environmentalVariables.Add((key, value));
                    }
                },
            };

            foreach (var option in runOptions)
            {
                options.Add(option);
            }

            return options;
        }

        public override void Validate()
        {
            base.Validate();

            if (!Directory.Exists(AppPackagePath))
            {
                throw new ArgumentException($"Failed to find the app bundle at {AppPackagePath}");
            }

            if (RunTargets.Count == 0)
            {
                throw new ArgumentException(
                    "No targets specified. At least one target must be provided. Available targets are:" +
                    GetAllowedValues(t => t.AsString(), invalidValues: TestTarget.None) +
                    Environment.NewLine + Environment.NewLine +
                    "You can also specify desired iOS/tvOS/WatchOS version. Example: ios-simulator-64_13.4");
            }

            if (!File.Exists(MlaunchPath))
            {
#if DEBUG
                string message = $"Failed to find mlaunch at {MlaunchPath}. " +
                    $"Make sure you specify --mlaunch or set the {EnvironmentVariables.Names.MLAUNCH_PATH} env var. " +
                    $"See README.md for more information";
#else
                string message = $"Failed to find mlaunch at {MlaunchPath}";
#endif
                throw new ArgumentException(message);
            }

            if (XcodeRoot != null && !Directory.Exists(XcodeRoot))
            {
                throw new ArgumentException($"Failed to find Xcode root at {XcodeRoot}");
            }
        }
    }
}
