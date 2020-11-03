// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.DotNet.XHarness.Common.Logging;

#nullable enable
namespace Microsoft.DotNet.XHarness.Common.Execution
{
    public class MacOSProcessManager : UnixProcessManager, IMacOSProcessManager
    {
        #region Private variables

        private static readonly Lazy<string> s_autoDetectedXcodeRoot = new Lazy<string>(DetectXcodePath, LazyThreadSafetyMode.PublicationOnly);
        private readonly string? _xcodeRoot;
        private Version? _xcode_version;

        #endregion

        public MacOSProcessManager(string? xcodeRoot = null)
        {
            _xcodeRoot = xcodeRoot;
        }

        #region IMacOSProcessManager implementation

        public string XcodeRoot => _xcodeRoot ?? s_autoDetectedXcodeRoot.Value;
        public Version XcodeVersion
        {
            get
            {
                if (_xcode_version == null)
                {
                    var doc = new XmlDocument();
                    var plistPath = Path.Combine(XcodeRoot, "Contents", "version.plist");

                    try
                    {
                        doc.Load(plistPath);
                        _xcode_version = Version.Parse(doc.SelectSingleNode("//key[text() = 'CFBundleShortVersionString']/following-sibling::string").InnerText);
                    }
                    catch (IOException)
                    {
                        throw new Exception(
                            $"Failed to find Xcode! Version.plist missing at {plistPath}. " +
                            "Please make sure xcode-select is set up, or the path to Xcode is supplied as an argument.");
                    }
                }
                return _xcode_version;
            }
        }

        public Task<ProcessExecutionResult> ExecuteXcodeCommandAsync(string executable, IList<string> args, ILog log, TimeSpan timeout)
        {
            var filename = Path.Combine(XcodeRoot, "Contents", "Developer", "usr", "bin", executable);
            return ExecuteCommandAsync(filename, args, log, timeout: timeout);
        }

        #endregion

        [DllImport("/usr/lib/libc.dylib")]
        private static extern int kill(int pid, int sig);

        #region Override methods

        protected override int Kill(int pid, int sig) => kill(pid, sig);

        #endregion

        #region Private methods

        private static string DetectXcodePath()
        {
            using var process = new Process();
            process.StartInfo.FileName = "xcode-select";
            process.StartInfo.Arguments = "-p";

            var log = new MemoryLog();
            var stdout = new MemoryLog() { Timestamp = false };
            var stderr = new ConsoleLog();
            var timeout = TimeSpan.FromSeconds(30);

            var result = RunAsyncInternal(
                    process,
                    log,
                    stdout,
                    stderr,
                    (pid, signal) => kill(pid, signal),
                    (log, pid) => GetChildProcessIdsInternal(log, pid),
                    timeout)
                .GetAwaiter().GetResult();

            if (!result.Succeeded)
            {
                throw new Exception("Failed to detect Xcode path from xcode-select!");
            }

            // Something like /Applications/Xcode114.app/Contents/Developers
            var xcodeRoot = stdout.ToString().Trim();

            if (string.IsNullOrEmpty(xcodeRoot))
            {
                throw new Exception("Failed to detect Xcode path from xcode-select!");
            }

            // We need /Applications/Xcode114.app only
            // should never be null, if it is return an ""
            return Path.GetDirectoryName(Path.GetDirectoryName(xcodeRoot)) ?? string.Empty;
        }

        #endregion
    }
}
