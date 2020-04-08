// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI.Common;
using Microsoft.Extensions.Logging;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.iOS
{
    /// <summary>
    /// Command which executes a given, already-packaged iOS application, waits on it and returns status based on the outcome.
    /// </summary>
    internal class iOSTestCommand : TestCommand
    {
        private readonly iOSTestCommandArguments _arguments = new iOSTestCommandArguments();
        protected override ITestCommandArguments TestArguments => _arguments;

        public iOSTestCommand() : base()
        {
            Options = new OptionSet() {
                "usage: ios test [OPTIONS]",
                "",
                "Packaging command that will create a iOS/tvOS/watchOS or macOS application that can be used to run NUnit or XUnit-based test dlls",
            };

            foreach (var option in CommonOptions)
            {
                Options.Add(option);
            }
        }

        protected override Task<int> InvokeInternal()
        {
            _log.LogInformation($"iOS Test command called:");
            _log.LogInformation($"  App: {_arguments.AppPackagePath}");
            _log.LogInformation($"  Targets: {string.Join(',', _arguments.Targets)}");
            _log.LogInformation($"  Output Directory: {_arguments.OutputDirectory}");
            _log.LogInformation($"  Working Directory: {_arguments.WorkingDirectory}");
            _log.LogInformation($"  Timeout: {_arguments.Timeout.TotalSeconds}s");

            return Task.FromResult(0);
        }
    }
}
