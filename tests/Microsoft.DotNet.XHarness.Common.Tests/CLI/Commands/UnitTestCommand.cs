// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.Common.CLI.Commands;
using Microsoft.Extensions.Logging;

#nullable enable
namespace Microsoft.DotNet.XHarness.Common.Tests.Utilities
{
    internal class UnitTestCommand<TArguments> : XHarnessCommand where TArguments : XHarnessCommandArguments
    {
        protected override string CommandUsage => "test";

        protected override string CommandDescription => "unit test command";

        public bool CommandRun { get; private set; }

        public IEnumerable<string> PassThroughArgs => PassThroughArguments;

        public IEnumerable<string> ExtraArgs => ExtraArguments;

        private readonly TArguments _arguments;
        protected override XHarnessCommandArguments Arguments => _arguments;

        public UnitTestCommand(TArguments arguments, bool allowExtraArgs) : base("unit-test", allowExtraArgs)
        {
            _arguments = arguments;
        }

        protected override Task<ExitCode> InvokeInternal(ILogger logger)
        {
            CommandRun = true;
            return Task.FromResult(ExitCode.SUCCESS);
        }
    }
}
