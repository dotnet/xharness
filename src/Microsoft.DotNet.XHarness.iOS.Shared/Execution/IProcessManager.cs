// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution.Mlaunch;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Execution
{

    public class ProcessExecutionResult
    {
        public bool TimedOut { get; set; }
        public int ExitCode { get; set; }
        public bool Succeeded => !TimedOut && ExitCode == 0;
    }

    // interface that helps to manage the different processes in the app.
    public interface IProcessManager
    {
        string XcodeRoot { get; }
        string MlaunchPath { get; }
        Version XcodeVersion { get; }

        Task<ProcessExecutionResult> ExecuteCommandAsync(string filename, IList<string> args, ILog log, TimeSpan timeout, Dictionary<string, string> environment_variables = null, CancellationToken? cancellationToken = null);
        Task<ProcessExecutionResult> ExecuteCommandAsync(string filename, IList<string> args, ILog log, ILog stdoutLog, ILog stderrLog, TimeSpan timeout, Dictionary<string, string> environment_variables = null, CancellationToken? cancellationToken = null);
        Task<ProcessExecutionResult> ExecuteCommandAsync(MlaunchArguments args, ILog log, TimeSpan timeout, Dictionary<string, string> environment_variables = null, CancellationToken? cancellation_token = null);
        Task<ProcessExecutionResult> ExecuteXcodeCommandAsync(string executable, IList<string> args, ILog log, TimeSpan timeout);
        Task<ProcessExecutionResult> RunAsync(Process process, ILog log, TimeSpan? timeout = null, Dictionary<string, string> environment_variables = null, CancellationToken? cancellation_token = null, bool? diagnostics = null);
        Task<ProcessExecutionResult> RunAsync(Process process, MlaunchArguments args, ILog log, TimeSpan? timeout = null, Dictionary<string, string> environment_variables = null, CancellationToken? cancellation_token = null, bool? diagnostics = null);
        Task<ProcessExecutionResult> RunAsync(Process process, ILog log, ILog stdoutLog, ILog stderrLog, TimeSpan? timeout = null, Dictionary<string, string> environment_variables = null, CancellationToken? cancellation_token = null, bool? diagnostics = null);
        Task KillTreeAsync(Process process, ILog log, bool? diagnostics = true);
        Task KillTreeAsync(int pid, ILog log, bool? diagnostics = true);
    }
}
