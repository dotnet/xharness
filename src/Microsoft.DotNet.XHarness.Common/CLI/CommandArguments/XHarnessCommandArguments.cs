// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.XHarness.Common.CLI.CommandArguments
{
    public interface IXHarnessCommandArguments
    {
        VerbosityArgument Verbosity { get; }
        HelpArgument ShowHelp { get; }
        IEnumerable<ArgumentDefinition> GetCommandArguments();
    }

    public abstract class XHarnessCommandArguments : IXHarnessCommandArguments
    {
        public VerbosityArgument Verbosity { get; } = new();
        public HelpArgument ShowHelp { get; } = new();

        public IEnumerable<ArgumentDefinition> GetCommandArguments() => GetArguments().Concat(new ArgumentDefinition[]
        {
            Verbosity,
            ShowHelp,
        });

        /// <summary>
        /// Returns additional option for your specific command.
        /// </summary>
        protected abstract IEnumerable<ArgumentDefinition> GetArguments();
    }
}
