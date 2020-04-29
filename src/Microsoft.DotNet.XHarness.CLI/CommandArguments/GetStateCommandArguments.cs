// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments
{
    internal abstract class GetStateCommandArguments : XHarnessCommandArguments
    {
        protected abstract string BaseCommand { get; }

        protected override OptionSet GetOptions()
        {
            var options = new OptionSet
            {
                $"usage: {BaseCommand} state",
                "",
                "Print information about the current machine, such as host machine info and device status",
            };

            foreach (var option in base.GetOptions())
            {
                options.Add(option);
            }

            return options;
        }
    }
}
