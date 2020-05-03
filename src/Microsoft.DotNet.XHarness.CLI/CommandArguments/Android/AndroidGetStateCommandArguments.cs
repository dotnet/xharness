// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Android
{
    internal class AndroidGetStateCommandArguments : GetStateCommandArguments
    {
        public override OptionSet GetOptions()
        {
            var options = new OptionSet
            {
                $"usage: android state",
                "",
                "Print information about the current machine, such as host machine info and device status",
            };

            foreach (var option in base.GetOptions())
            {
                options.Add(option);
            }

            return options;
        }

        public override void Validate()
        {
        }
    }
}
