// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.Common
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
                    _log.LogError("Invalid arguments:");
                    foreach (string error in validationErrors)
                    {
                        _log.LogError("  - " + error);
                    }

                    return 1;
                }

                return InvokeInternal().GetAwaiter().GetResult();
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
                    .AddConsole()
                    .AddFilter(
                    (level) =>
                    {
                        return level >= verbosity;
                    });
            });
            _log = _logFactory.CreateLogger(name);
        }

        protected abstract Task<int> InvokeInternal();
    }
}
