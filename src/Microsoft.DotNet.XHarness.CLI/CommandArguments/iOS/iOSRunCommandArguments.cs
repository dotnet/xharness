// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.iOS
{
    internal class iOSRunCommandArguments : iOSAppRunArguments
    {
        /// <summary>
        /// Expected result code the app should return. Defaults to 0.
        /// </summary>
        public int ExpectedExitCode { get; set; } = 0;

        protected override OptionSet GetCommandOptions()
        {
            var options = base.GetCommandOptions();

            var runOptions = new OptionSet
            {
                {
                    "expected-exit-code:", "If specified, sets the expected exit code of the app that is being run.",
                    v =>
                    {
                        if (int.TryParse(v, out var number))
                        {
                            ExpectedExitCode = number;
                            return;
                        }

                        throw new ArgumentException("expected-exit-code must be an integer");
                    }
                },
            };

            foreach (var option in runOptions)
            {
                options.Add(option);
            }

            return options;
        }
    }
}
