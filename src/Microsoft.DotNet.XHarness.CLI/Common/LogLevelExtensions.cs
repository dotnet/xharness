// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Common
{
    internal static class LogLevelExtensions
    {
        public static int ToInt(this LogLevel level) => level switch
        {
            LogLevel.Trace => 6,
            LogLevel.Debug => 5,
            LogLevel.Information => 4,
            LogLevel.Warning => 3,
            LogLevel.Error => 2,
            LogLevel.Critical => 1,
            LogLevel.None => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(level)),
        };
    }
}
