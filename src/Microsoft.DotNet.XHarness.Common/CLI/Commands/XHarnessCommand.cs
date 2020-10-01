// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.Common.CLI.Commands
{
    public abstract class XHarnessCommand : Command
    {
        /// <summary>
        /// The verbatim "--" argument used for pass-through args is removed by Mono.Options when parsing CommandSets,
        /// so in Program.cs, we temporarily replace it with this string and then recognize it back here.
        /// </summary>
        public const string VerbatimArgumentPlaceholder = "[[%verbatim_argument%]]";

        private readonly bool _allowsExtraArgs;

        /// <summary>
        /// Will be printed in the header when help is invoked.
        /// Example: 'ios package [OPTIONS]'
        /// </summary>
        protected abstract string CommandUsage { get; }

        /// <summary>
        /// Will be printed in the header when help is invoked.
        /// Example: 'Allows to package DLLs into an app bundle'
        /// </summary>
        protected abstract string CommandDescription { get; }

        protected abstract XHarnessCommandArguments Arguments { get; }

        /// <summary>
        /// Contains all arguments after the verbatim "--" argument.
        ///
        /// Example:
        ///   > xharness ios test --arg1=value1 -- --foo -v
        ///   Will contain [ "foo", "v" ]
        /// </summary>
        protected IEnumerable<string> PassThroughArguments { get; private set; } = Enumerable.Empty<string>();

        /// <summary>
        /// Extra arguments parsed to the command (if the command allows it).
        /// </summary>
        protected IEnumerable<string> ExtraArguments { get; private set; } = Enumerable.Empty<string>();

        protected XHarnessCommand(string name, bool allowsExtraArgs, string? help = null) : base(name, help)
        {
            _allowsExtraArgs = allowsExtraArgs;
        }

        public sealed override int Invoke(IEnumerable<string> arguments)
        {
            OptionSet options = Arguments.GetOptions();

            using var parseFactory = CreateLoggerFactory(Arguments.Verbosity);
            var parseLogger = parseFactory.CreateLogger(Name);

            try
            {
                var regularArguments = arguments.TakeWhile(arg => arg != VerbatimArgumentPlaceholder);
                if (regularArguments.Count() < arguments.Count())
                {
                    PassThroughArguments = arguments.Skip(regularArguments.Count() + 1);
                    arguments = regularArguments;
                }

                var extra = options.Parse(arguments);

                if (extra.Count > 0)
                {
                    if (_allowsExtraArgs)
                    {
                        ExtraArguments = extra;
                    }
                    else
                    {
                        throw new ArgumentException($"Unknown arguments: {string.Join(" ", extra)}");
                    }
                }

                if (Arguments.ShowHelp)
                {
                    Console.WriteLine("usage: " + CommandUsage + Environment.NewLine + Environment.NewLine + CommandDescription + Environment.NewLine);
                    options.WriteOptionDescriptions(Console.Out);
                    return (int)ExitCode.HELP_SHOWN;
                }

                Arguments.Validate();
            }
            catch (ArgumentException e)
            {
                parseLogger.LogError(e.Message);

                if (Arguments.ShowHelp)
                {
                    options.WriteOptionDescriptions(Console.Out);
                }

                return (int)ExitCode.INVALID_ARGUMENTS;
            }
            catch (Exception e)
            {
                parseLogger.LogCritical("Unexpected failure argument: " + e);
                return (int)ExitCode.GENERAL_FAILURE;
            }

            try
            {
                using var factory = CreateLoggerFactory(Arguments.Verbosity);
                var logger = factory.CreateLogger(Name);

                return (int)InvokeInternal(logger).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                parseLogger.LogCritical(e.ToString());
                return (int)ExitCode.GENERAL_FAILURE;
            }
        }

        protected abstract Task<ExitCode> InvokeInternal(ILogger logger);

        private ILoggerFactory CreateLoggerFactory(LogLevel verbosity) => LoggerFactory.Create(builder =>
        {
            var disableColors = IsEnvVarTrue("XHARNESS_DISABLE_COLORED_OUTPUT");
            var timestampFormat = IsEnvVarTrue("XHARNESS_LOG_WITH_TIMESTAMPS") ? "[HH:mm:ss] " : null;

            var formatter = new XHarnessConsoleFormatter(disableColors, timestampFormat);

            builder
            .AddProvider(new XHarnessLoggerProvider(formatter))
            .SetMinimumLevel(verbosity);
        });

        private static bool IsEnvVarTrue(string varName) =>
            Environment.GetEnvironmentVariable(varName)?.ToLower().Equals("true") ?? false;
    }

    public class XHarnessLoggerProvider : ILoggerProvider
    {
        private readonly ConsoleFormatter _formatter;
        private readonly ConcurrentDictionary<string, XHarnessConsoleLogger> _loggers = new ConcurrentDictionary<string, XHarnessConsoleLogger>();

        public XHarnessLoggerProvider(ConsoleFormatter formatter)
        {
            _formatter = formatter;
        }

        public void Dispose() { }

        public ILogger CreateLogger(string categoryName) =>
            _loggers.GetOrAdd(categoryName, loggerName => new XHarnessConsoleLogger(categoryName, _formatter));

        public class XHarnessConsoleLogger : ILogger
        {
            private readonly string _categoryName;
            private readonly ConsoleFormatter _formatter;

            [ThreadStatic]
            private static StringWriter? s_stringWriter;

            public XHarnessConsoleLogger(string categoryName, ConsoleFormatter formatter)
            {
                _categoryName = categoryName;
                _formatter = formatter;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                if (!IsEnabled(logLevel))
                {
                    return;
                }

                s_stringWriter ??= new StringWriter();

                LogEntry<TState> logEntry = new LogEntry<TState>(logLevel, _categoryName, eventId, state, exception, formatter);
                _formatter.Write(in logEntry, null!, s_stringWriter);

                var sb = s_stringWriter.GetStringBuilder();
                if (sb.Length == 0)
                {
                    return;
                }

                Console.WriteLine(sb);
            }

            public IDisposable BeginScope<TState>(TState state) => null!;

            public bool IsEnabled(LogLevel logLevel) => true;
        }
    }

    /*public class XHarnessLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private readonly ConsoleLoggerOptions _options;
        private readonly ConcurrentDictionary<string, ConsoleLogger> _loggers;
        private readonly ConsoleLoggerProcessor _messageQueue;

        private IExternalScopeProvider _scopeProvider = NullExternalScopeProvider.Instance;

        /// <summary>
        /// Creates an instance of <see cref="XHarnessLoggerProvider"/>.
        /// </summary>
        /// <param name="options">The options to create <see cref="ConsoleLogger"/> instances with.</param>
        public XHarnessLoggerProvider(ConsoleLoggerOptions options)
            : this(options, Enumerable.Empty<ConsoleFormatter>()) { }

        /// <summary>
        /// Creates an instance of <see cref="XHarnessLoggerProvider"/>.
        /// </summary>
        /// <param name="options">The options to create <see cref="ConsoleLogger"/> instances with.</param>
        /// <param name="formatters">Log formatters added for <see cref="ConsoleLogger"/> insteaces.</param>
        public XHarnessLoggerProvider(ConsoleLoggerOptions options, IEnumerable<ConsoleFormatter> formatters)
        {
            _options = options;
            _loggers = new ConcurrentDictionary<string, ConsoleLogger>();

            ReloadLoggerOptions(options.CurrentValue);

            _messageQueue = new ConsoleLoggerProcessor();
            if (DoesConsoleSupportAnsi())
            {
                _messageQueue.Console = new AnsiLogConsole();
                _messageQueue.ErrorConsole = new AnsiLogConsole(stdErr: true);
            }
            else
            {
                _messageQueue.Console = new AnsiParsingLogConsole();
                _messageQueue.ErrorConsole = new AnsiParsingLogConsole(stdErr: true);
            }

            if (options.FormatterName == null || !_formatters.TryGetValue(options.FormatterName, out ConsoleFormatter logFormatter))
            {
#pragma warning disable CS0618
                logFormatter = options.Format switch
                {
                    ConsoleLoggerFormat.Systemd => _formatters[ConsoleFormatterNames.Systemd],
                    _ => _formatters[ConsoleFormatterNames.Simple],
                };
                if (options.FormatterName == null)
                {
                    UpdateFormatterOptions(logFormatter, options);
                }
#pragma warning restore CS0618
            }
        }

        private static bool DoesConsoleSupportAnsi()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return true;
            }
            // for Windows, check the console mode
            var stdOutHandle = Interop.Kernel32.GetStdHandle(Interop.Kernel32.STD_OUTPUT_HANDLE);
            if (!Interop.Kernel32.GetConsoleMode(stdOutHandle, out int consoleMode))
            {
                return false;
            }

            return (consoleMode & Interop.Kernel32.ENABLE_VIRTUAL_TERMINAL_PROCESSING) == Interop.Kernel32.ENABLE_VIRTUAL_TERMINAL_PROCESSING;
        }

        // warning:  ReloadLoggerOptions can be called before the ctor completed,... before registering all of the state used in this method need to be initialized
        private void ReloadLoggerOptions(ConsoleLoggerOptions options)
        {
        }

        /// <inheritdoc />
        public ILogger CreateLogger(string name)
        {
            if (_options.CurrentValue.FormatterName == null || !_formatters.TryGetValue(_options.CurrentValue.FormatterName, out ConsoleFormatter logFormatter))
            {
#pragma warning disable CS0618
                logFormatter = _options.CurrentValue.Format switch
                {
                    ConsoleLoggerFormat.Systemd => _formatters[ConsoleFormatterNames.Systemd],
                    _ => _formatters[ConsoleFormatterNames.Simple],
                };
                if (_options.CurrentValue.FormatterName == null)
                {
                    UpdateFormatterOptions(logFormatter, _options.CurrentValue);
                }
#pragma warning disable CS0618
            }

            return _loggers.GetOrAdd(name, loggerName => new ConsoleLogger(name, _messageQueue)
            {
                Options = _options.CurrentValue,
                ScopeProvider = _scopeProvider,
                Formatter = logFormatter,
            });
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _messageQueue.Dispose();
        }

        /// <inheritdoc />
        public void SetScopeProvider(IExternalScopeProvider scopeProvider)
        {
            _scopeProvider = scopeProvider;

            foreach (KeyValuePair<string, ConsoleLogger> logger in _loggers)
            {
                logger.Value.ScopeProvider = _scopeProvider;
            }
        }
    }*/
}
