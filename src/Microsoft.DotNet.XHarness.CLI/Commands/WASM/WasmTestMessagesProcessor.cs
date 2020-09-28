// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Wasm
{
    public class WasmTestMessagesProcessor
    {
        private StreamWriter? _xmlResultsFileWriter;
        private readonly StreamWriter _stdoutFileWriter;
        private readonly string _xmlResultsFilePath;

        private readonly ILogger _logger;

        public WasmTestMessagesProcessor(string xmlResultsFilePath, string stdoutFilePath, ILogger logger)
        {
            this._xmlResultsFilePath = xmlResultsFilePath;
            this._stdoutFileWriter = File.CreateText(stdoutFilePath);
            this._stdoutFileWriter.AutoFlush = true;
            this._logger = logger;
        }

        public void Invoke(string line)
        {
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
                    _logger.LogInformation(line);
                }

                _stdoutFileWriter.Write(line);
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
                _xmlResultsFileWriter.Write(line);
            }
        }
    }
}
