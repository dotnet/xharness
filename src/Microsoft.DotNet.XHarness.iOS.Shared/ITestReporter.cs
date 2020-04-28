﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;

namespace Microsoft.DotNet.XHarness.iOS.Shared
{

    // interface that represents a class that know how to parse the results from an app run.
    public interface ITestReporter
    {

        ILog CallbackLog { get; }

        bool? Success { get; }
        CancellationToken CancellationToken { get; }

        void LaunchCallback(Task<bool> launchResult);

        Task CollectSimulatorResult(Task<ProcessExecutionResult> processExecution);
        Task CollectDeviceResult(Task<ProcessExecutionResult> processExecution);
        Task<(TestExecutingResult ExecutingResult, string ResultMessage)> ParseResult();
    }
}
