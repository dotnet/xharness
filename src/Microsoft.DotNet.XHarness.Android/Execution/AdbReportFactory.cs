// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.Android.Execution
{
    internal class AdbReportFactory
    {
        internal static BaseReportManager CreateReportManager(ILogger log, int api)
        {
            if (api > 23) return new BaseReportManager(log);
            else return new OldReportManager(log);
        }
    }
}
