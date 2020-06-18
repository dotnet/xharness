// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared.Execution.Mlaunch
{
    public class MLaunchProcessManager : MacOSProcessManager, IMLaunchProcessManager
    {
        #region IMLaunchProcessManager implementation
        public string MlaunchPath { get; }

        public MLaunchProcessManager(string? xcodeRoot = null, string mlaunchPath = "/Library/Frameworks/Xamarin.iOS.framework/Versions/Current/bin/mlaunch") : base(xcodeRoot)
        {
            MlaunchPath = mlaunchPath;
        }

        public async Task<ProcessExecutionResult> ExecuteCommandAsync(
            MlaunchArguments args,
            ILog log,
            TimeSpan timeout,
            Dictionary<string, string>? environmentVariables = null,
            CancellationToken? cancellationToken = null)
        {
            using var p = new Process();
            return await RunAsync(p, args, log, log, log, timeout, environmentVariables, cancellationToken);
        }

        public async Task<ProcessExecutionResult> ExecuteCommandAsync(
            MlaunchArguments args,
            ILog log,
            ILog stdout,
            ILog stderr,
            TimeSpan timeout,
            Dictionary<string, string>? environmentVariables = null,
            CancellationToken? cancellationToken = null)
        {
            using var p = new Process();
            return await RunAsync(p, args, log, stdout, stderr, timeout, environmentVariables, cancellationToken);
        }

        public Task<ProcessExecutionResult> RunAsync(
            Process process,
            MlaunchArguments args,
            ILog log,
            TimeSpan? timeout = null,
            Dictionary<string, string>? environmentVariables = null,
            CancellationToken? cancellationToken = null,
            bool? diagnostics = null)
        {
            if (!args.Any(a => a is SdkRootArgument))
            {
                args = new MlaunchArguments(args.Prepend(new SdkRootArgument(XcodeRoot)).ToArray());
            }

            process.StartInfo.FileName = MlaunchPath;
            process.StartInfo.Arguments = args.AsCommandLine();

            return RunAsync(process, log, log, log, timeout, environmentVariables, cancellationToken, diagnostics);
        }

        public Task<ProcessExecutionResult> RunAsync(
            Process process,
            MlaunchArguments args,
            ILog log,
            ILog stdout,
            ILog stderr,
            TimeSpan? timeout = null,
            Dictionary<string, string>? environmentVariables = null,
            CancellationToken? cancellationToken = null,
            bool? diagnostics = null)
        {
            if (!args.Any(a => a is SdkRootArgument))
            {
                args = new MlaunchArguments(args.Prepend(new SdkRootArgument(XcodeRoot)).ToArray());
            }

            process.StartInfo.FileName = MlaunchPath;
            process.StartInfo.Arguments = args.AsCommandLine();

            return RunAsync(process, log, stdout, stderr, timeout, environmentVariables, cancellationToken, diagnostics);
        }

        #endregion
    }
}
