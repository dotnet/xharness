﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.Common.CLI.CommandArguments
{
    public class VerbosityArgument : ArgumentDefinition
    {
        public LogLevel Value { get; private set; } = LogLevel.Information;

        public VerbosityArgument()
            : base("verbosity:|v:", "Verbosity level - defaults to 'Information' if not specified. If passed without value, 'Debug' is assumed (highest)")
        {
        }

        public override void Action(string argumentValue)
        {
            Value = string.IsNullOrEmpty(argumentValue) ? LogLevel.Debug : ParseArgument<LogLevel>("verbosity", argumentValue);
        }
    }
}
