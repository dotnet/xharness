// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.Common
{
    internal abstract class PackageCommand : XHarnessCommand
    {
        protected override ICommandArguments Arguments => PackageArguments;
        protected abstract IPackageCommandArguments PackageArguments { get; }

        protected readonly OptionSet CommonOptions;

        public PackageCommand() : base("package")
        {
            CommonOptions = new OptionSet
            {
                { "name=|n=", "Name of the test application",  v => PackageArguments.AppPackageName = v},
                { "output-directory=|o=", "Directory in which the resulting package will be outputted", v => PackageArguments.OutputDirectory = v},
                { "working-directory=|w=", "Directory in which the resulting package will be outputted", v => PackageArguments.WorkingDirectory = v},
                { "help|h", "Show this message", v => ShowHelp = v != null }
            };
        }
    }
}
