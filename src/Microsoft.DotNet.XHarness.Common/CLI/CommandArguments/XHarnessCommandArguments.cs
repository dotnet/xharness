// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.Common.CLI.CommandArguments
{
    public abstract class XHarnessCommandArguments
    {
        public LogLevel Verbosity { get; set; } = LogLevel.Information;

        public bool ShowHelp { get; set; } = false;

        /// <summary>
        /// Collects together all options from this class and from GetCommandOptions()
        /// Options from this class are appended to the bottom of the list.
        /// </summary>
        /// <remarks>
        /// If you don't want the verbosity option, feel free to override this method
        /// but the help option is important for the help command to work correctly.
        /// </remarks>
        public OptionSet GetOptions()
        {
            var options = new OptionSet();

            foreach (var option in GetArguments())
            {
                options.Add(option.Prototype, option.Description, option.Action);
            }

            options.Add("verbosity:|v:", "Verbosity level - defaults to 'Information' if not specified. If passed without value, 'Debug' is assumed (highest)",
                v => Verbosity = string.IsNullOrEmpty(v) ? LogLevel.Debug : ParseArgument<LogLevel>("verbosity", v));

            options.Add("help|h", v => ShowHelp = v != null);

            return options;
        }

        /// <summary>
        /// Returns additional option for your specific command.
        /// </summary>
        protected abstract IEnumerable<ArgumentDefinition> GetArguments();
    }
}
