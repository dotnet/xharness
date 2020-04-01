// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.Common
{
    internal abstract class XHarnessCommand : Command
    {
        protected abstract ICommandArguments Arguments { get; }

        protected bool ShowHelp = false;

        protected XHarnessCommand(string name) : base(name)
        {
        }

        public override sealed int Invoke(IEnumerable<string> arguments)
        {
            var extra = Options.Parse(arguments);

            if (ShowHelp)
            {
                Options.WriteOptionDescriptions(Console.Out);
                return 0;
            }

            if (extra.Count > 0)
            {
                Console.Error.WriteLine($"Unknown arguments{string.Join(" ", extra)}");
                Options.WriteOptionDescriptions(Console.Out);
                return 1;
            }

            if (!Arguments.TryValidate(out var errors))
            {
                Console.Error.WriteLine("Invalid arguments:");
                foreach (string error in errors)
                {
                    Console.Error.WriteLine("  - " + error);
                }

                return 1;
            }

            return InvokeInternal().GetAwaiter().GetResult();
        }

        protected abstract Task<int> InvokeInternal();
    }
}
