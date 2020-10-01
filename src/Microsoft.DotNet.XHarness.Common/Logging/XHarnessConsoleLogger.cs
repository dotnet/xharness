// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.Common.Logging
{
    internal class XHarnessConsoleLogger : ILogger
    {
        private const string LogLevelSeparator = ": ";
        private const string DefaultForegroundColor = "\x1B[39m\x1B[22m"; // reset to default foreground color
        private const string DefaultBackgroundColor = "\x1B[49m"; // reset to the background color

        private readonly XHarnessConsoleLoggerOptions _options;
        private readonly string _messagePadding;
        private readonly string _newLineWithMessagePadding;

        public XHarnessConsoleLogger(XHarnessConsoleLoggerOptions options)
        {
            _messagePadding = new string(' ', GetLogLevelString(LogLevel.Information).Length + LogLevelSeparator.Length + (options.TimestampFormat?.Length ?? 0));
            _newLineWithMessagePadding = Environment.NewLine + _messagePadding;
            _options = options;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            string message = formatter(state, exception);
            if (exception == null && message == null)
            {
                return;
            }

            var colors = GetLogLevelConsoleColors(logLevel);
            string logLevelString = GetLogLevelString(logLevel);

            var logMessage = new StringBuilder();

            if (_options.TimestampFormat != null)
            {
                string timestamp = DateTimeOffset.Now.ToString(_options.TimestampFormat);
                logMessage.Append(timestamp);
            }

            // Log level
            AppendColoredMessage(logMessage, logLevelString, colors.Background, colors.Foreground);

            // The colon after log level ": "
            logMessage.Append(LogLevelSeparator);

            // Rest of the message
            AppendPaddedMessage(logMessage, message, false /* first padding  */);

            // Log an exception
            // Example:
            // System.InvalidOperationException
            //    at Namespace.Class.Function() in File:line X
            if (exception != null)
            {
                AppendPaddedMessage(logMessage, exception.ToString());
            }

            Console.WriteLine(logMessage);
        }

        public IDisposable BeginScope<TState>(TState state) => null!;

        public bool IsEnabled(LogLevel logLevel) => true;

        private void AppendPaddedMessage(StringBuilder logMessage, string message, bool padFirstLine = true)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            if (padFirstLine)
            {
                logMessage.Append(_messagePadding);
            }

            logMessage.Append(message.Replace(Environment.NewLine, _newLineWithMessagePadding));
        }

        private static void AppendColoredMessage(StringBuilder logMessage, string message, ConsoleColor? background, ConsoleColor? foreground)
        {
            // Order: backgroundcolor, foregroundcolor, Message, reset foregroundcolor, reset backgroundcolor
            if (background.HasValue)
            {
                logMessage.Append(GetBackgroundColorEscapeCode(background.Value));
            }

            if (foreground.HasValue)
            {
                logMessage.Append(GetForegroundColorEscapeCode(foreground.Value));
            }

            logMessage.Append(message);

            if (foreground.HasValue)
            {
                logMessage.Append(DefaultForegroundColor); // reset to default foreground color
            }

            if (background.HasValue)
            {
                logMessage.Append(DefaultBackgroundColor); // reset to the background color
            }
        }

        private static string GetLogLevelString(LogLevel logLevel) => logLevel switch
        {
            LogLevel.Trace => "trce",
            LogLevel.Debug => "dbug",
            LogLevel.Information => "info",
            LogLevel.Warning => "warn",
            LogLevel.Error => "fail",
            LogLevel.Critical => "crit",
            _ => throw new ArgumentOutOfRangeException(nameof(logLevel))
        };

        private (ConsoleColor? Foreground, ConsoleColor? Background) GetLogLevelConsoleColors(LogLevel logLevel)
        {
            if (_options.DisableColors)
            {
                return (null, null);
            }

            // We must explicitly set the background color if we are setting the foreground color,
            // since just setting one can look bad on the users console.
            return logLevel switch
            {
                LogLevel.Trace => (ConsoleColor.Gray, ConsoleColor.Black),
                LogLevel.Debug => (ConsoleColor.Gray, ConsoleColor.Black),
                LogLevel.Information => (ConsoleColor.DarkGreen, ConsoleColor.Black),
                LogLevel.Warning => (ConsoleColor.Yellow, ConsoleColor.Black),
                LogLevel.Error => (ConsoleColor.Black, ConsoleColor.DarkRed),
                LogLevel.Critical => (ConsoleColor.White, ConsoleColor.DarkRed),
                _ => (null, null)
            };
        }

        private static string GetForegroundColorEscapeCode(ConsoleColor color) => color switch
        {
            ConsoleColor.Black => "\x1B[30m",
            ConsoleColor.DarkRed => "\x1B[31m",
            ConsoleColor.DarkGreen => "\x1B[32m",
            ConsoleColor.DarkYellow => "\x1B[33m",
            ConsoleColor.DarkBlue => "\x1B[34m",
            ConsoleColor.DarkMagenta => "\x1B[35m",
            ConsoleColor.DarkCyan => "\x1B[36m",
            ConsoleColor.Gray => "\x1B[37m",
            ConsoleColor.Red => "\x1B[1m\x1B[31m",
            ConsoleColor.Green => "\x1B[1m\x1B[32m",
            ConsoleColor.Yellow => "\x1B[1m\x1B[33m",
            ConsoleColor.Blue => "\x1B[1m\x1B[34m",
            ConsoleColor.Magenta => "\x1B[1m\x1B[35m",
            ConsoleColor.Cyan => "\x1B[1m\x1B[36m",
            ConsoleColor.White => "\x1B[1m\x1B[37m",
            _ => DefaultForegroundColor // default foreground color
        };

        private static string GetBackgroundColorEscapeCode(ConsoleColor color) => color switch
        {
            ConsoleColor.Black => "\x1B[40m",
            ConsoleColor.DarkRed => "\x1B[41m",
            ConsoleColor.DarkGreen => "\x1B[42m",
            ConsoleColor.DarkYellow => "\x1B[43m",
            ConsoleColor.DarkBlue => "\x1B[44m",
            ConsoleColor.DarkMagenta => "\x1B[45m",
            ConsoleColor.DarkCyan => "\x1B[46m",
            ConsoleColor.Gray => "\x1B[47m",
            _ => DefaultBackgroundColor // Use default background color
        };
    }
}
