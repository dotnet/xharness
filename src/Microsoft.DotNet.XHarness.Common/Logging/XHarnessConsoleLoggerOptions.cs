// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.Common.Logging
{
    internal class XHarnessConsoleLoggerOptions
    {
        public XHarnessConsoleLoggerOptions(bool disableColors, string? timestampFormat)
        {
            DisableColors = disableColors;
            TimestampFormat = timestampFormat;
        }

        public bool DisableColors { get; }


        public string? TimestampFormat { get; }
    }
}
