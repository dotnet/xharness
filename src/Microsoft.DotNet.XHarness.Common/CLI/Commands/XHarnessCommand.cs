// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;
using Microsoft.Extensions.Logging;
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

        protected bool UseSingleLineLogging { get; set; } = true;

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
            builder
            .AddSimpleConsole(options =>
            {
                options.SingleLine = UseSingleLineLogging;

                if (Environment.GetEnvironmentVariable("XHARNESS_DISABLE_COLORED_OUTPUT")?.ToLower().Equals("true") ?? false)
                {
                    options.ColorBehavior = Extensions.Logging.Console.LoggerColorBehavior.Disabled;
                }
                else
                {
                    options.ColorBehavior = Extensions.Logging.Console.LoggerColorBehavior.Enabled;
                }

                if (Environment.GetEnvironmentVariable("XHARNESS_LOG_WITH_TIMESTAMPS")?.ToLower().Equals("true") ?? false)
                {
                    options.TimestampFormat = "[HH:mm:ss] ";
                }
            })
            .SetMinimumLevel(verbosity);
        });
    }
}
