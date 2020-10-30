// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Wasm
{
    public class WasmTestMessagesProcessor
    {
        private StreamWriter? _xmlResultsFileWriter;
        private readonly StreamWriter _stdoutFileWriter;
        private readonly string _xmlResultsFilePath;

        private readonly ILogger _logger;

        public TaskCompletionSource<bool> WasmExitReceivedTcs { get; } = new TaskCompletionSource<bool>();

        public WasmTestMessagesProcessor(string xmlResultsFilePath, string stdoutFilePath, ILogger logger)
        {
            this._xmlResultsFilePath = xmlResultsFilePath;
            this._stdoutFileWriter = File.CreateText(stdoutFilePath);
            this._stdoutFileWriter.AutoFlush = true;
            this._logger = logger;
        }

        public void Invoke(string message)
        {
            try
            {
                InvokeInternal(message);
            }
            catch (Exception ex) when (WasmExitReceivedTcs.Task.IsCompletedSuccessfully)
            {
                _logger.LogWarning($"Test has returned a result already, but the message processor threw {ex.GetType()},"
                                    + $" while logging the message: {message}{Environment.NewLine}{ex}");
            }
        }

        private void InvokeInternal(string message)
        {
            WasmLogMessage? logMessage = null;
            string line;

            if (message.StartsWith("{"))
            {
                try
                {
                    logMessage = JsonSerializer.Deserialize<WasmLogMessage>(message);
                    line = logMessage?.payload ?? message.TrimEnd();
                }
                catch (JsonException)
                {
                    line = message.TrimEnd();
                }
            }
            else
            {
                line = message.TrimEnd();
            }

            if (_xmlResultsFileWriter == null)
            {
                if (line.Contains("STARTRESULTXML"))
                {
                    _xmlResultsFileWriter = File.CreateText(_xmlResultsFilePath);
                    return;
                }
                else if (line.StartsWith("[PASS]") || line.StartsWith("[SKIP]"))
                {
                    _logger.LogDebug(line);
                }
                else if (line.StartsWith("[FAIL]"))
                {
                    _logger.LogError(line);
                }
                else
                {
                    switch (logMessage?.method?.ToLowerInvariant())
                    {
                        case "console.debug": _logger.LogDebug(line); break;
                        case "console.error": _logger.LogError(line); break;
                        case "console.warn": _logger.LogWarning(line); break;
                        case "console.trace": _logger.LogTrace(line); break;

                        case "console.log":
                        default: _logger.LogInformation(line); break;
                    }
                }

                if (_stdoutFileWriter.BaseStream.CanWrite)
                    _stdoutFileWriter.WriteLine(line);
            }
            else
            {
                if (line.Contains("ENDRESULTXML"))
                {
                    _xmlResultsFileWriter.Flush();
                    _xmlResultsFileWriter.Dispose();
                    _xmlResultsFileWriter = null;
                    return;
                }

                if (_xmlResultsFileWriter?.BaseStream.CanWrite == true)
                    _xmlResultsFileWriter.WriteLine(line);
            }

            // the test runner writes this as the last line,
            // after the tests have run, and the xml results file
            // has been written to the console
            if (line.StartsWith("WASM EXIT"))
            {
                WasmExitReceivedTcs.SetResult(true);
            }
        }
    }
}
