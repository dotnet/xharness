// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Android;
using Microsoft.Extensions.Logging;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Android
{
    internal class AndroidGetStateCommand : GetStateCommand
    {
        public AndroidGetStateCommand()
        {
            Options = new OptionSet() {
                "usage: android state",
                "",
                "Print information about the current machine, such as host machine info and device status"
            };
        }

        protected override Task<ExitCode> InvokeInternal()
        {
            _log.LogInformation("Getting state of ADB and attached Android device(s)");
            try
            {
                var runner = new AdbRunner(_log);
                string state = runner.GetAdbState();
                if (string.IsNullOrEmpty(state))
                {
                    state = "No device attached";
                }
                _log.LogInformation($"ADB Version info:{Environment.NewLine}{runner.GetAdbVersion()}");
                _log.LogInformation($"ADB State ('device' if physically attached):{Environment.NewLine}{state}");
                return Task.FromResult(ExitCode.SUCCESS);
            }
            catch (Exception toLog)
            {
                _log.LogCritical(toLog, $"Error: {toLog.Message}");
                return Task.FromResult(ExitCode.GENERAL_FAILURE);
            }
        }
    }
}
