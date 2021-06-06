// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;
using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments
{
    internal abstract class AppRunCommandArguments : XHarnessCommandArguments
    {
        public AppPathArgument AppBundlePath { get; } = new(); // throw new ArgumentException("You must provide a path for the app that will be tested.")
        public OutputDirectoryArgument OutputDirectory { get; } = new(); // throw new ArgumentException("You must provide an output directory where results will be stored.")
        public TimeoutArgument Timeout { get; } = new(TimeSpan.FromMinutes(15));

        public override void Validate()
        {
            if (!Directory.Exists(OutputDirectory.Path ?? throw new ArgumentNullException("Output directory was not set")))
            {
                Directory.CreateDirectory(OutputDirectory.Path);
            }
        }
    }
}
