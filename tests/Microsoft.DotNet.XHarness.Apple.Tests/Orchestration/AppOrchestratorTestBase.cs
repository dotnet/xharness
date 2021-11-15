// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Moq;

namespace Microsoft.DotNet.XHarness.Apple.Tests.AppOperations;

public abstract class AppOrchestratorTestBase : OrchestratorTestBase
{
    protected readonly Mock<IMlaunchProcessManager> _processManager;
    protected readonly Mock<IAppRunnerFactory> _appRunnerFactory;

    public AppOrchestratorTestBase()
    {
        _processManager = new();
        _appRunnerFactory = new();

        // Prepare succeeding install/uninstall as we don't care about those in the test/run tests
        _appInstaller.SetReturnsDefault(Task.FromResult(new ProcessExecutionResult
        {
            ExitCode = 0,
            TimedOut = false,
        }));

        _appUninstaller.SetReturnsDefault(Task.FromResult(new ProcessExecutionResult
        {
            ExitCode = 0,
            TimedOut = false,
        }));
    }
}
