// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.Common.CLI.Commands;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Apple
{
    internal class AppleGetDeviceCommand : XHarnessCommand
    {
        private readonly AppleGetDeviceArguments _arguments = new();

        protected override XHarnessCommandArguments Arguments => _arguments;

        protected override string CommandUsage { get; } = "apple device [TARGET] [OPTIONS]";

        private const string CommandHelp = "Finds the UDID of a device/simulator for given target";
        protected override string CommandDescription { get; } = @$"
{CommandHelp}
 
Arguments:
";

        public AppleGetDeviceCommand() : base("device", true, CommandHelp)
        {
        }

        protected override async Task<ExitCode> InvokeInternal(ILogger logger)
        {
            var processManager = new MlaunchProcessManager(_arguments.XcodeRoot, _arguments.MlaunchPath);

            var log = new CallbackLog(m => logger.LogDebug(m));
            TestTargetOs target;

            try
            {
                target = ParseTarget();
            }
            catch (Exception e)
            {
                logger.LogError(e.Message);
                return ExitCode.INVALID_ARGUMENTS;
            }

            try
            {
                if (target.Platform.IsSimulator())
                {
                    var simulatorLoader = new SimulatorLoader(processManager);

                    // TODO: Handle companion mode
                    var (simulator, _) = await simulatorLoader.FindSimulators(target, log, 3, true);
                    Console.WriteLine(simulator.UDID);
                }
                else
                {
                    var hardwareDeviceLoader = new HardwareDeviceLoader(processManager);
                    var device = await hardwareDeviceLoader.FindDevice(target.Platform.ToRunMode(), log, false);
                    Console.WriteLine(device.UDID);
                }
            }
            catch (Exception e)
            {
                logger.LogError(e.ToString());
                return ExitCode.DEVICE_NOT_FOUND;
            }

            return ExitCode.SUCCESS;
        }

        private TestTargetOs ParseTarget()
        {
            if (ExtraArguments.Count() != 1)
            {
                throw CreateException("You have to specify one target platform");
            }

            string targetName = ExtraArguments.First();
            TestTargetOs target;

            try
            {
                target = targetName.ParseAsAppRunnerTargetOs();
            }
            catch (Exception)
            {
                throw CreateException($"Failed to parse test target '{targetName}'");
            }

            if (target.Platform == TestTarget.MacCatalyst)
            {
                throw CreateException("Target maccatalyst is not supported for this command");
            }

            return target;
        }

        private static Exception CreateException(string message) => new ArgumentException(
            $"{message}. Available targets are:" +
            XHarnessCommandArguments.GetAllowedValues(t => t.AsString(), TestTarget.None, TestTarget.MacCatalyst) +
            Environment.NewLine + Environment.NewLine +
            "You can also specify desired iOS/tvOS/WatchOS version. Example: ios-simulator-64_13.4");
    }
}
