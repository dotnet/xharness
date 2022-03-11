// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Android;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.AndroidHeadless;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.AndroidHeadless;

internal class AndroidHeadlessAdbCommand : XHarnessCommand<AndroidHeadlessAdbCommandArguments>
{
    private const string Description = "Invoke bundled adb with given arguments";
    protected override string CommandUsage { get; } = "android-headless adb [OPTIONS] -- [ADB ARGUMENTS]";
    protected override string CommandDescription => Description;

    protected override AndroidHeadlessAdbCommandArguments Arguments { get; } = new();

    public AndroidHeadlessAdbCommand() : base(TargetPlatform.Android, "adb", false, new ServiceCollection(), Description)
    {
    }

    protected override Task<ExitCode> InvokeInternal(ILogger logger)
    {
        if (!PassThroughArguments.Any())
        {
            logger.LogError("Please provide delimeter '--' followed by arguments for ADB:" + Environment.NewLine +
                $"    {CommandUsage}" + Environment.NewLine +
                $"Example:" + Environment.NewLine +
                $"    android-headless adb --timeout 00:01:30 -- devices -l");

            return Task.FromResult(ExitCode.INVALID_ARGUMENTS);
        }

        var runner = new AdbRunner(logger);

        try
        {
            var result = runner.RunAdbCommand(PassThroughArguments, Arguments.Timeout);

            Console.Write(result.StandardOutput);
            Console.Error.Write(result.StandardError);

            return Task.FromResult((ExitCode)result.ExitCode);
        }
        catch (Exception toLog)
        {
            logger.LogCritical(toLog, $"Error: {toLog.Message}");
            return Task.FromResult(ExitCode.GENERAL_FAILURE);
        }
    }
}
