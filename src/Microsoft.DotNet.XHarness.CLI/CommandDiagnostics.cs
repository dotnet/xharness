// Licensed to the.NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.XHarness.CLI.Commands;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI
{
    /// <summary>
    /// Class responsible for gathering of diagnostics data and saving them into a file.
    /// </summary>
    internal class CommandDiagnostics
    {
        private readonly ILogger _logger;
        private readonly Stopwatch _timer = Stopwatch.StartNew();

        public TargetPlatform Platform { get; }

        public string Command { get; }

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

            }
            catch (Exception e)
            {
                _logger.LogError("Failed to save diagnostics data into '{pathToFile}': {error}", targetFile, e);
            }
        }
    }
}
