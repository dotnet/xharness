﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.Android.Execution
{
    internal class OldReportManager : BaseReportManager
    {
        public OldReportManager(ILogger log) : base(log)
        {
        }

        public override void DumpBugReport(AdbRunner runner, string outputFilePath)
        {
            // give some time for bug report to be available
            Thread.Sleep(3000);

            var result = runner.RunAdbCommand($"bugreport", TimeSpan.FromMinutes(5));

            if (result.ExitCode != 0)
            {
                // Could throw here, but it would tear down a possibly otherwise acceptable execution.
                Logger.LogError($"Error getting ADB bugreport:{Environment.NewLine}{result}");
            }
            else
            {
                File.WriteAllText($"{outputFilePath}.txt", result.StandardOutput);
                Logger.LogInformation($"Wrote ADB bugreport to {outputFilePath}.txt");
            }
        }
    }
}
