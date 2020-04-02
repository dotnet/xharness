// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Common
{
    internal interface ICommandArguments
    {
        bool TryValidate([NotNullWhen(true)]out IEnumerable<string> errors);

        /// <summary>
        /// Minimum level at which logging statements will be emitted to the console
        /// </summary>
        public LogLevel Verbosity { get; set; }
    }

    internal interface ITestCommandArguments : ICommandArguments
    {
        /// <summary>
        /// Path to packaged app
        /// </summary>
        string AppPackagePath { get; set; }

        /// <summary>
        /// List of targets to test
        /// </summary>
        IReadOnlyCollection<string> Targets { get; set; }

        /// <summary>
        /// How long XHarness should wait until a test execution completes before clean up (kill running apps, uninstall, etc)
        /// </summary>
        TimeSpan Timeout { get; set; }

        /// <summary>
        /// Path where the outputs of execution will be stored
        /// </summary>
        string OutputDirectory { get; set; }

        /// <summary>
        /// Path where run logs will hbe stored and projects
        /// </summary>
        string WorkingDirectory { get; set; }

    }
}
