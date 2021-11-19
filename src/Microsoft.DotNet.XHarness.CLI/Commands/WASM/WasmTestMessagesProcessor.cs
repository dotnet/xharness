// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

#nullable enable
namespace Microsoft.DotNet.XHarness.CLI.Commands.Wasm
{
    public class WasmTestMessagesProcessor
    {
        private MemoryStream? _xmlResultsMemoryStream;
        private StreamWriter? _xmlResultsFileWriter;
        private readonly StreamWriter _stdoutFileWriter;
        private readonly string _xmlResultsFilePath;

        private readonly ILogger _logger;
        private readonly Lazy<ErrorPatternScanner>? _errorScanner;

        public string? LineThatMatchedErrorPattern { get; private set; }

        public TaskCompletionSource<bool> WasmExitReceivedTcs { get; } = new TaskCompletionSource<bool>();

        public WasmTestMessagesProcessor(string xmlResultsFilePath, string stdoutFilePath, ILogger logger, string? errorPatternsFile)
        {
            _xmlResultsFilePath = xmlResultsFilePath;
            _stdoutFileWriter = File.CreateText(stdoutFilePath);
            _stdoutFileWriter.AutoFlush = true;
            _logger = logger;

            if (errorPatternsFile != null)
            {
                if (!File.Exists(errorPatternsFile))
                    throw new ArgumentException($"Cannot find error patterns file {errorPatternsFile}");

                _errorScanner = new Lazy<ErrorPatternScanner>(() => new ErrorPatternScanner(errorPatternsFile, logger));
            }
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
                    _logger.LogDebug("Reached start of testResults.xml");
                    _xmlResultsMemoryStream = new MemoryStream();
                    _xmlResultsFileWriter = new StreamWriter(_xmlResultsMemoryStream);
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
                    ScanMessage(line);

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
                    _logger.LogDebug($"Reached end of {_xmlResultsFilePath}");
                    _xmlResultsFileWriter.Flush();
                    _xmlResultsMemoryStream!.Seek(0, SeekOrigin.Begin);
                    try
                    {
                        // we validate it, to make sure it's not corrupted.
                        var testResults = XElement.Load(_xmlResultsMemoryStream);
                        using (var testResultsFile = File.CreateText(_xmlResultsFilePath))
                        {
                            testResults.Save(testResultsFile);
                            _logger.LogInformation($"Written {_xmlResultsMemoryStream.Length} original bytes of {_xmlResultsFilePath} as {testResultsFile.BaseStream.Length} formatted bytes");
                        };
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error while saving testResults.xml {ex}");
                        throw;
                    }
                    _xmlResultsFileWriter.Dispose();
                    _xmlResultsFileWriter = null;
                    _xmlResultsMemoryStream.Dispose();
                    _xmlResultsMemoryStream = null;
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
                _logger.LogDebug("Reached wasm exit");
                WasmExitReceivedTcs.SetResult(true);
            }
        }

        private void ScanMessage(string message)
        {
            if (LineThatMatchedErrorPattern != null || _errorScanner == null)
                return;

            if (_errorScanner.Value.IsError(message, out string? _))
                LineThatMatchedErrorPattern = message;
        }

        public void ProcessErrorMessage(string message)
        {
            _logger.LogError(message);
            ScanMessage(message);
        }
    }
}
