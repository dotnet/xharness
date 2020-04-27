// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments
{
    internal interface ICommandArguments
    {
        IList<string> GetValidationErrors();

        /// <summary>
        /// Minimum level at which logging statements will be emitted to the console
        /// </summary>
        public LogLevel Verbosity { get; set; }
    }
}
