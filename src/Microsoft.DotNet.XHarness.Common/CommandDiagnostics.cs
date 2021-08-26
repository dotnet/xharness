// Licensed to the.NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.Common
{
    public interface IDiagnosticsData
    {
        ExitCode ExitCode { get; set; }

        /// <summary>
        /// Original target the user specified when executing the command.
        /// </summary>
        string? OriginalTarget { get; set; }

        /// <summary>
        /// Actual test target (simulator, device) that was used for the run.
        /// This should include OS version of the target.
        /// </summary>
        string? Target { get; set; }
    }

    /// <summary>
    /// Class responsible for gathering of diagnostics data and saving them into a file.
    /// </summary>
    public class CommandDiagnostics : IDiagnosticsData
    {
        private readonly ILogger _logger;
        private readonly Stopwatch _timer = Stopwatch.StartNew();

        public TargetPlatform Platform { get; }

        public string Command { get; }

        public ExitCode ExitCode { get; set; }

        public string? OriginalTarget { get; set; }

        public string? Target { get; set; }

        public int Duration => (int)_timer.Elapsed.TotalSeconds;

        public CommandDiagnostics(ILogger logger, TargetPlatform platform, string command)
        {
            _logger = logger;
            Platform = platform;
            Command = command;
        }

        public void SaveData(string targetFile)
        {
            try
            {
                // TODO
            }
            catch (Exception e)
            {
                _logger.LogError("Failed to save diagnostics data to '{pathToFile}': {error}", targetFile, e);
            }
        }
    }
}
