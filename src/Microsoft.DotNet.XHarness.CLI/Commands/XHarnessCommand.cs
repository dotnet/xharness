// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI.CommandArguments;
using Microsoft.Extensions.Logging;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.Commands
{
    internal abstract class XHarnessCommand : Command
    {
        protected abstract XHarnessCommandArguments Arguments { get; }

        protected XHarnessCommand(string name) : base(name)
        {
        }

        public override sealed int Invoke(IEnumerable<string> arguments)
        {
            OptionSet options = Arguments.GetOptions();

            try
            {
                var extra = options.Parse(arguments);

                if (extra.Count > 0)
                {
                    throw new ArgumentException($"Unknown arguments{string.Join(" ", extra)}");
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Invalid arguments: " + e.Message);
                Options.WriteOptionDescriptions(Console.Out);
                return (int)ExitCode.INVALID_ARGUMENTS;
            }

            if (Arguments.ShowHelp)
            {
                Options.WriteOptionDescriptions(Console.Out);
                return (int)ExitCode.HELP_SHOWN;
            }

            var logger = CreateLogger(Arguments.Verbosity);

            return (int)InvokeInternal(logger).GetAwaiter().GetResult();
        }

        protected abstract Task<ExitCode> InvokeInternal(ILogger logger);

        private ILogger CreateLogger(LogLevel verbosity)
        {
            return LoggerFactory
                .Create(builder =>
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
                    .AddFilter(level => level >= verbosity);
                })
                .CreateLogger(Name);
        }
    }
}
