﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Execution;
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
        public string MlaunchPath { get; set; } = MacOSProcessManager.DetectMlaunchPath();

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

        /// <summary>
        /// Test target.
        /// </summary>
        public TestTargetOs Target { get; private set; } = null!;

        protected override OptionSet GetCommandOptions()
        {
            var options = base.GetCommandOptions();

            var runOptions = new OptionSet
            {
                {
                    "target=|t=", "Test target (device/simulator and OS)",
                    v =>
                    {
                        try
                        {
                            Target = v.ParseAsAppRunnerTargetOs();
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            throw new ArgumentException(
                                $"Failed to parse test target '{v}'. Available targets are:" +
                                GetAllowedValues(t => t.AsString(), invalidValues: TestTarget.None) +
                                Environment.NewLine + Environment.NewLine +
                                "You can also specify desired OS version, e.g. ios-simulator-64_13.4");
                        }
                    }
                },
                {
                    "xcode=", "Path where Xcode is installed",
                    v => XcodeRoot = RootPath(v)
                },
                {
                    "mlaunch=", "Path to the mlaunch binary",
                    v => MlaunchPath = RootPath(v)
                },
                {
                    "device=", "Name or UDID of a simulator/device you wish to target",
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
                    v =>
                    {
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

            if (Target == null)
            {
                throw new ArgumentException("No test target specified");
            }

            if (!File.Exists(MlaunchPath))
            {
                throw new ArgumentException(
                    $"Failed to find mlaunch at {MlaunchPath}. " +
                    $"Make sure you specify --mlaunch or set the {EnvironmentVariables.Names.MLAUNCH_PATH} env var. " +
                    $"See README.md for more information");
            }

            if (XcodeRoot != null && !Directory.Exists(XcodeRoot))
            {
                throw new ArgumentException($"Failed to find Xcode root at {XcodeRoot}");
            }
        }
    }
}
