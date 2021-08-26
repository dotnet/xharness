// Licensed to the.NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
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
            _timer.Stop();

            var options = new JsonSerializerOptions
            {
#if DEBUG
                WriteIndented = true,
#else
                WriteIndented = false,
#endif
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            try
            {
                // Either append current data to the JSON array or create a new file
                if (File.Exists(targetFile))
                {
                    var data = JsonDocument.Parse(File.ReadAllText(targetFile));

                    using var fileStream = new FileStream(targetFile, FileMode.Create, FileAccess.Write);
                    using var jsonWriter = new Utf8JsonWriter(fileStream);

                    jsonWriter.WriteStartArray();

                    // Copy the existing elements without going into details of what they are
                    var newData = new List<JsonElement>();
                    var enumerator = data.RootElement.EnumerateArray();
                    while (enumerator.MoveNext())
                    {
                        enumerator.Current.WriteTo(jsonWriter);
                    }

                    // New element
                    JsonSerializer.Serialize(jsonWriter, this, options);

                    jsonWriter.WriteEndArray();
                }
                else
                {
                    var data = new[]
                    {
                        this
                    };
                    string json = JsonSerializer.Serialize(data, options);
                    File.WriteAllText(targetFile, JsonSerializer.Serialize(data, options));
                }
            }
            catch (Exception e)
            {
                _logger.LogError("Failed to save diagnostics data to '{pathToFile}': {error}", targetFile, e);
            }
        }
    }
}
