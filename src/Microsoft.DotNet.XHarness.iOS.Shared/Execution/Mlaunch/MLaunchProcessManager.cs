// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared.Execution.Mlaunch
{
    public class MLaunchProcessManager : MacOSProcessManager, IMLaunchProcessManager
    {

        #region Private variables

        static readonly Lazy<string> s_autoDetectedXcodeRoot = new Lazy<string>(DetectXcodePath, LazyThreadSafetyMode.PublicationOnly);
        readonly string? _xcodeRoot;
        Version? _xcode_version;

        #endregion

        #region IMLaunchProcessManager implementation

        public string XcodeRoot => _xcodeRoot ?? s_autoDetectedXcodeRoot.Value;
        public string MlaunchPath { get; }
        public Version XcodeVersion
        {
            get
            {
                if (_xcode_version == null)
                {
                    var doc = new XmlDocument();
                    doc.Load(Path.Combine(XcodeRoot, "Contents", "version.plist"));
                    _xcode_version = Version.Parse(doc.SelectSingleNode("//key[text() = 'CFBundleShortVersionString']/following-sibling::string").InnerText);
                }
                return _xcode_version;
            }
        }

        public MLaunchProcessManager(string? xcodeRoot = null, string mlaunchPath = "/Library/Frameworks/Xamarin.iOS.framework/Versions/Current/bin/mlaunch")
        {
            this._xcodeRoot = xcodeRoot;
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

        public Task<ProcessExecutionResult> ExecuteXcodeCommandAsync(string executable, IList<string> args, ILog log, TimeSpan timeout)
        {
            string filename = Path.Combine(XcodeRoot, "Contents", "Developer", "usr", "bin", executable);
            return ExecuteCommandAsync(filename, args, log, timeout: timeout);
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
                args.Prepend(new SdkRootArgument(XcodeRoot));

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
                args.Prepend(new SdkRootArgument(XcodeRoot));

            process.StartInfo.FileName = MlaunchPath;
            process.StartInfo.Arguments = args.AsCommandLine();

            return RunAsync(process, log, stdout, stderr, timeout, environmentVariables, cancellationToken, diagnostics);
        }

        #endregion

        #region Private methods

        static string DetectXcodePath()
        {
            using var process = new Process();
            process.StartInfo.FileName = "xcode-select";
            process.StartInfo.Arguments = "-p";

            var log = new MemoryLog();
            var stdout = new MemoryLog() { Timestamp = false };
            var stderr = new ConsoleLog();
            var timeout = TimeSpan.FromSeconds(30);

            var result = RunAsyncInternal(
                process: process,
                log: log,
                stdout: stdout,
                stderr: stderr,
                kill: (p,s) => kill(p, s),
                getChildrenPS: (i, l) => GetChildrenPSInternal(i, l),
                timeout: timeout).GetAwaiter().GetResult();

            if (!result.Succeeded)
                throw new Exception("Failed to detect Xcode path from xcode-select!");

            // Something like /Applications/Xcode114.app/Contents/Developers
            var xcodeRoot = stdout.ToString().Trim();

            if (string.IsNullOrEmpty(xcodeRoot))
                throw new Exception("Failed to detect Xcode path from xcode-select!");

            // We need /Applications/Xcode114.app only
            // should never be null, if it is return an ""
            return Path.GetDirectoryName(Path.GetDirectoryName(xcodeRoot)) ?? string.Empty;
        }

        #endregion
    }
}
