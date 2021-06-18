// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;

namespace Microsoft.DotNet.XHarness.Apple
{
    public abstract class AppRunnerBase
    {
        private const string SystemLogPath = "/var/log/system.log";

        private readonly IProcessManager _processManager;
        private readonly ICaptureLogFactory _captureLogFactory;
        private readonly ILogs _logs;
        private readonly IHelpers _helpers;
        private readonly IFileBackedLog _mainLog;

        private bool _appEndSignalDetected = false;

        protected AppRunnerBase(
            IProcessManager processManager,
            ICaptureLogFactory captureLogFactory,
            ILogs logs,
            IFileBackedLog mainLog,
            IHelpers helpers,
            Action<string>? logCallback = null)
        {
            _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
            _captureLogFactory = captureLogFactory ?? throw new ArgumentNullException(nameof(captureLogFactory));
            _logs = logs ?? throw new ArgumentNullException(nameof(logs));
            _helpers = helpers ?? throw new ArgumentNullException(nameof(helpers));

            if (logCallback == null)
            {
                _mainLog = mainLog ?? throw new ArgumentNullException(nameof(mainLog));
            }
            else
            {
                // create using the main as the default log
                _mainLog = Log.CreateReadableAggregatedLog(mainLog, new CallbackLog(logCallback));
            }
        }

        protected async Task<ProcessExecutionResult> RunMacCatalystApp(
            AppBundleInformation appInfo,
            ILog appOutputLog,
            TimeSpan timeout,
            IEnumerable<string> extraArguments,
            Dictionary<string, string> environmentVariables,
            CancellationToken cancellationToken)
        {
            using var systemLog = _captureLogFactory.Create(
                path: _logs.CreateFile("MacCatalyst.system.log", LogType.SystemLog),
                systemLogPath: SystemLogPath,
                entireFile: false,
                LogType.SystemLog);

            // We need to make the binary executable
            var binaryPath = Path.Combine(appInfo.AppPath, "Contents", "MacOS", appInfo.BundleExecutable ?? appInfo.AppName);
            if (File.Exists(binaryPath))
            {
                await _processManager.ExecuteCommandAsync("chmod", new[] { "+x", binaryPath }, _mainLog, TimeSpan.FromSeconds(10), cancellationToken: cancellationToken);
            }

            // On Big Sur it seems like the launch services database is not updated fast enough after we do some I/O with the app bundle.
            // Force registration for the app by running
            // /System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister -f /path/to/app.app
            var lsRegisterPath = @"/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister";
            await _processManager.ExecuteCommandAsync(lsRegisterPath, new[] {"-f", appInfo.LaunchAppPath }, _mainLog, TimeSpan.FromSeconds(10), cancellationToken: cancellationToken);

            var arguments = new List<string>
            {
                "-W",
                appInfo.LaunchAppPath
            };

            arguments.AddRange(extraArguments);

            systemLog.StartCapture();

            try
            {
                return await _processManager.ExecuteCommandAsync(
                    "open",
                    arguments,
                    _mainLog,
                    appOutputLog,
                    appOutputLog,
                    timeout,
                    environmentVariables,
                    cancellationToken);
            }
            finally
            {
                systemLog.StopCapture(waitIfEmpty: TimeSpan.FromSeconds(10));
            }
        }

        /// <summary>
        /// User can pass additional arguments after the -- which get turned to environmental variables.
        /// </summary>
        /// <param name="envVariables">Environmental variables where the arguments are added</param>
        /// <param name="variables">Variables to set</param>
        protected void AddExtraEnvVars(Dictionary<string, string> envVariables, IEnumerable<(string, string)> variables)
        {
            using (var enumerator = variables.GetEnumerator())
            while (enumerator.MoveNext())
            {
                var (name, value) = enumerator.Current;
                if (envVariables.ContainsKey(name))
                {
                    _mainLog.WriteLine($"Environmental variable {name} is already passed to the application to drive test run, skipping..");
                    continue;
                }

                envVariables[name] = value;
            }
        }

        protected string WatchForAppEndTag(
            out string tag,
            ref IFileBackedLog appLog,
            ref CancellationToken cancellationToken)
        {
            tag = _helpers.GenerateGuid().ToString();
            var appEndDetected = new CancellationTokenSource();
            var appEndScanner = new ScanLog(tag, () =>
            {
                _mainLog.WriteLine("Detected test end tag in application's output");
                _appEndSignalDetected = true;
                appEndDetected.Cancel();
            });

            // We need to check for test end tag since iOS 14+ doesn't send the pidDiedCallback event to mlaunch
            appLog = Log.CreateReadableAggregatedLog(appLog, appEndScanner);
            cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, appEndDetected.Token).Token;

            return tag;
        }

        protected async Task<ProcessExecutionResult> RunAndWatchForAppSignal(Func<Task<ProcessExecutionResult>> action)
        {
            var result = await action();

            // When signal is detected, we cancel the call above via the cancellation token so we need to fix the result
            if (_appEndSignalDetected)
            {
                result.TimedOut = false;
                result.ExitCode = 0;
            }

            return result;
        }
    }
}
