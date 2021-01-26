// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;

namespace Microsoft.DotNet.XHarness.Apple
{
    public interface IExitCodeDetector
    {
        int DetectExitCode(AppBundleInformation appBundleInfo, IReadableLog systemLog);
    }

    public class ExitCodeDetector : IExitCodeDetector
    {
        public int DetectExitCode(AppBundleInformation appBundleInfo, IReadableLog systemLog)
        {
            using var reader = systemLog.GetReader();
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();

                // This will be improved when we find out it differes for new iOS versions
                if (line.Contains("UIKitApplication:") && line.Contains(appBundleInfo.AppName) && line.Contains("Service exited with abnormal code"))
                {
                    var regex = new Regex(" (\\-?[0-9]+)$");
                    var match = regex.Match(line);

                    if (match.Success && int.TryParse(match.Captures.First().Value, out var exitCode))
                    {
                        return exitCode;
                    }
                }
            }

            return 0;
        }
    }
}
