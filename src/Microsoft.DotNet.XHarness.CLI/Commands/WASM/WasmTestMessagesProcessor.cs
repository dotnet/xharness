// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Wasm
{
    public class WasmTestMessagesProcessor
    {
        private StreamWriter? _xmlResultsFileWriter;
        private readonly string _xmlResultsFilePath;
        private bool _hasWasmStdoutPrefix = false;

        private readonly ILogger _logger;

        public WasmTestMessagesProcessor(string xmlResultsFilePath, ILogger logger)
        {
            this._xmlResultsFilePath = xmlResultsFilePath;
            this._logger = logger;
        }

        public void Invoke(string line)
        {
            if (_xmlResultsFileWriter == null)
            {
                if (line.Contains("STARTRESULTXML"))
                {
                    _xmlResultsFileWriter = File.CreateText(_xmlResultsFilePath);
                    _hasWasmStdoutPrefix = line.StartsWith("WASM: ");
                    return;
                }
                else if (line.Contains("Tests run:"))
                {
                    _logger.LogInformation(line);
                }
                else
                {
                    _logger.LogDebug(line);
                }
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
                _xmlResultsFileWriter.WriteLine(_hasWasmStdoutPrefix ? line.Substring(6) : line);
            }
        }
    }
}
