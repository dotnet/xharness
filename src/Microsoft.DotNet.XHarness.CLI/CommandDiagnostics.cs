// Licensed to the.NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.XHarness.CLI
{
    /// <summary>
    /// Class responsible for gathering of diagnostics data and saving them into a file.
    /// </summary>
    internal class CommandDiagnostics
    {
        private readonly List<DiagnosticData> _data = new();

        public DiagnosticData Current => _data.LastOrDefault() ?? throw new InvalidOperationException("You must start a new record first");

        public DiagnosticData CreateRecord(DiagnosticData.TargetPlatform platform, string command)
        {
            var data = new DiagnosticData
            {
                Platform = platform,
                Command = command,
            };

            _data.Add(data);

            return data;
        }

        public void SaveData(string targetFile)
        {
        }
    }

    internal class DiagnosticData
    {
        private readonly Stopwatch _timer;

        public TargetPlatform? Platform { get; set; }

        public string? Command { get; set; }

        public int Duration => (int)_timer.Elapsed.TotalSeconds;

        public DiagnosticData()
        {
            _timer = Stopwatch.StartNew();
        }

        internal enum TargetPlatform
        {
            Android,
            Apple,
        }
    }
}
