// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.Commands.iOS
{
    internal class iOSGetStateCommand : GetStateCommand
    {
        public iOSGetStateCommand() : base()
        {
            Options = new OptionSet() {
                "usage: ios state",
                "",
                "Print information about the current machine, such as host machine info and device status"
            };
        }

        protected override Task<ExitCode> InvokeInternal()
        {
            _log.LogInformation("iOS state command called (no args supported)");

            return Task.FromResult(ExitCode.SUCCESS);
        }
    }
}
