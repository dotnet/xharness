// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;
using Microsoft.Extensions.Logging;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.Common.CLI.Commands
{
    internal abstract class XHarnessCommand : Command
    {
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

        protected XHarnessCommand(string name, string? help = null) : base(name, help)
        {
        }

        public sealed override int Invoke(IEnumerable<string> arguments)
        {
            OptionSet options = Arguments.GetOptions();

            using var parseFactory = CreateLoggerFactory(Arguments.Verbosity);
            var parseLogger = parseFactory.CreateLogger(Name);

            try
            {
                var extra = options.Parse(arguments);

                if (extra.Count > 0)
                {
                    throw new ArgumentException($"Unknown arguments: {string.Join(" ", extra)}");
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
            .AddConsole(options =>
            {
                if (Environment.GetEnvironmentVariable("XHARNESS_DISABLE_COLORED_OUTPUT")?.ToLower().Equals("true") ?? false)
                {
                    options.DisableColors = true;
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
