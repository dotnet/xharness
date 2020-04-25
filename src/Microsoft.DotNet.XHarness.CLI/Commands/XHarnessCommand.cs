// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI.CommandArguments;
using Microsoft.Extensions.Logging;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.Commands
{
    internal abstract class XHarnessCommand : Command
    {
        protected abstract ICommandArguments Arguments { get; }

        protected bool ShowHelp = false;

        protected ILogger _log;
        protected ILoggerFactory _logFactory;
        protected string _name;

        protected XHarnessCommand(string name) : base(name)
        {
            _name = name;
        }

        public override sealed int Invoke(IEnumerable<string> arguments)
        {
            try
            {
                var extra = Options.Parse(arguments);

                if (Arguments != null)
                {
                    InitializeLog(Arguments.Verbosity, _name);
                }
                else
                {
                    InitializeLog(LogLevel.Information, _name);
                }

                if (ShowHelp)
                {
                    Options.WriteOptionDescriptions(Console.Out);
                    return 0;
                }

                if (extra.Count > 0)
                {
                    _log.LogError($"Unknown arguments{string.Join(" ", extra)}");
                    Options.WriteOptionDescriptions(Console.Out);
                    return 1;
                }

                var validationErrors = Arguments?.GetValidationErrors();

                if (validationErrors?.Any() ?? false)
                {
                    var message = new StringBuilder("Invalid arguments:");
                    foreach (string error in validationErrors)
                    {
                        // errors can have more than one line, if the do, add
                        // some nice indentation
                        var lines = error.Split(Environment.NewLine);
                        if (lines.Length > 1)
                        {
                            // first line is in the same distance, rest have
                            // and indentation
                            message.Append(Environment.NewLine + "  - " + lines[0]);
                            for (int index = 1; index < lines.Length; index++)
                            {
                                message.Append($"{Environment.NewLine}\t{lines[index]}");
                            }
                        }
                        else
                        {
                            message.Append(Environment.NewLine + "  - " + error);
                        }
                    }

                    _log.LogError(message.ToString());

                    return 1;
                }

                return (int)InvokeInternal().GetAwaiter().GetResult();
            }
            finally
            {
                // Needed for quick execution to flush out all output.
                _logFactory.Dispose();
            }
        }

        private void InitializeLog(LogLevel verbosity, string name)
        {
            _logFactory = LoggerFactory.Create(builder =>
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
            });
            _log = _logFactory.CreateLogger(name);
        }

        protected abstract Task<ExitCode> InvokeInternal();
    }
}
