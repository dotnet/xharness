// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Microsoft.DotNet.XHarness.CLI.Commands;

public class TestMessagesProcessor
{
    private static Regex xmlRx = new Regex(@"^STARTRESULTXML ([0-9]*) ([^ ]*) ENDRESULTXML", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly StreamWriter _stdoutFileWriter;
    private readonly string _xmlResultsFilePath;
    private static TimeSpan s_logMessagesTimeout = TimeSpan.FromMinutes(2);

    private readonly ILogger _logger;
    private readonly Lazy<ErrorPatternScanner>? _errorScanner;
    private readonly SymbolicatorBase? _symbolicator;
    private readonly ChannelReader<(string, bool)> _channelReader;
    private readonly ChannelWriter<(string, bool)> _channelWriter;
    private readonly TaskCompletionSource _completed = new ();
    private bool _isRunning => !_completed.Task.IsCompleted;
    private bool _loggedProcessorStopped = false;

    public string? LineThatMatchedErrorPattern { get; private set; }

    // Set once `WASM EXIT` message is received
    public TaskCompletionSource WasmExitReceivedTcs { get; } = new ();

    public TestMessagesProcessor(string xmlResultsFilePath, string stdoutFilePath, ILogger logger, string? errorPatternsFile, SymbolicatorBase? symbolicator)
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

        _symbolicator = symbolicator;

        var channel = Channel.CreateUnbounded<(string, bool)>(new UnboundedChannelOptions { SingleReader = true });
        _channelWriter = channel.Writer;
        _channelReader = channel.Reader;
    }

    public async Task RunAsync(CancellationToken token)
    {
        try
        {
            await foreach ((string line, bool isError) in _channelReader.ReadAllAsync(token))
            {
                ProcessMessage(line, isError);
            }
            _completed.SetResult();
        }
        catch (Exception ex)
        {
            _channelWriter.TryComplete(ex);

            // surface the exception from task for this method
            // and from _completed
            _completed.SetException(ex);
            throw;
        }
    }

    public void Invoke(string message, bool isError = false)
    {
        WarnOnceIfStopped();

        if (_isRunning && _channelWriter.TryWrite((message, isError)))
            return;

        LogMessage(message.TrimEnd(), isError);
    }

    public Task InvokeAsync(string message, CancellationToken token, bool isError = false)
    {
        string? logMsg;
        try
        {
            WarnOnceIfStopped();
            if (_isRunning)
                return _channelWriter.WriteAsync((message, isError), token).AsTask();

            logMsg = message.TrimEnd();
        }
        catch (ChannelClosedException cce)
        {
            logMsg = $"Failed to write to the channel - {cce.Message}. Message: {message}";
        }

        LogMessage(logMsg, isError);
        return Task.CompletedTask;
    }

    private void WarnOnceIfStopped()
    {
        if (!_isRunning && !_loggedProcessorStopped)
        {
            _logger.LogWarning($"Message processor is not running anymore.");
            _loggedProcessorStopped = true;
        }
    }

    private void LogMessage(string message, bool isError)
    {
        if (isError)
            _logger.LogError(message);
        else
            _logger.LogInformation(message);
    }

    public async Task<ExitCode> CompleteAndFlushAsync(TimeSpan? timeout = null)
    {
        timeout ??= s_logMessagesTimeout;
        _logger.LogInformation($"Waiting to flush log messages with a timeout of {timeout.Value.TotalSeconds} secs ..");

        try
        {
            _channelWriter.TryComplete();
            await _completed.Task.WaitAsync(timeout.Value);
            return ExitCode.SUCCESS;
        }
        catch (TimeoutException)
        {
            _logger.LogError($"Flushing log messages timed out after {s_logMessagesTimeout.TotalSeconds}secs");
            return ExitCode.TIMED_OUT;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Flushing log messages failed with: {ex}. Ignoring.");
            return ExitCode.GENERAL_FAILURE;
        }
    }

    private void ProcessMessage(string message, bool isError = false)
    {
        LogMessage? logMessage = null;
        string line;

        if (message.StartsWith("{"))
        {
            try
            {
                logMessage = JsonSerializer.Deserialize<LogMessage>(message);
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

        var match = xmlRx.Match(line);
        if (match.Success)
        {
            var expectedLength = Int32.Parse(match.Groups[1].Value);
            using (var stream = new FileStream(_xmlResultsFilePath, FileMode.Create))
            {
                var bytes = System.Convert.FromBase64String(match.Groups[2].Value);
                stream.Write(bytes);
                if (bytes.Length == expectedLength)
                {
                    _logger.LogInformation($"Received expected {bytes.Length} of {_xmlResultsFilePath}");
                }
                else
                {
                    _logger.LogInformation($"Received {bytes.Length} of {_xmlResultsFilePath} but expected {expectedLength}");
                }
            }
        }
        else
        {
            if (line.StartsWith("[PASS]") || line.StartsWith("[SKIP]"))
            {
                _logger.LogDebug(line);
            }
            else if (line.StartsWith("[FAIL]"))
            {
                _logger.LogError(line);
            }
            else
            {
                ScanMessageForErrorPatterns(line);
                line = Symbolicate(line);

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

        // the test runner writes this as the last line,
        // after the tests have run, and the xml results file
        // has been written to the console
        if (line.StartsWith("WASM EXIT"))
        {
            _logger.LogDebug("Reached wasm exit");
            if (!WasmExitReceivedTcs.TrySetResult())
                _logger.LogDebug("Got a duplicate exit message.");
        }
    }

    private string Symbolicate(string msg)
    {
        if (_symbolicator is null)
            return msg;

        return _symbolicator.Symbolicate(msg);
    }

    private void ScanMessageForErrorPatterns(string message)
    {
        if (LineThatMatchedErrorPattern != null || _errorScanner == null)
            return;

        if (_errorScanner.Value.IsError(message, out string? _))
            LineThatMatchedErrorPattern = message;
    }

    public void ProcessErrorMessage(string message) => Invoke(message, isError: true);
}