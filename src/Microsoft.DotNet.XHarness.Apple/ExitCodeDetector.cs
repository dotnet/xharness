﻿// Licensed to the .NET Foundation under one or more agreements.
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

    public abstract class ExitCodeDetector : IExitCodeDetector
    {
        public int DetectExitCode(AppBundleInformation appBundleInfo, IReadableLog systemLog)
        {
            using var reader = systemLog.GetReader();
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();

                if (IsSignalLine(appBundleInfo, line))
                {
                    var match = ExitCodeRegex.Match(line);

                    if (match.Success && int.TryParse(match.Captures.First().Value, out var exitCode))
                    {
                        return exitCode;
                    }
                }
            }

            return 0;
        }

        protected abstract bool IsSignalLine(AppBundleInformation appBundleInfo, string logLine);

        protected virtual Regex ExitCodeRegex { get; } = new Regex(" (\\-?[0-9]+)$", RegexOptions.Compiled);
    }

    public class iOSExitCodeDetector : ExitCodeDetector
    {
        // Example line
        // Nov 18 04:31:44 ML-MacVM com.apple.CoreSimulator.SimDevice.2E1EE736-5672-4220-89B5-B7C77DB6AF18[55655] (UIKitApplication:net.dot.HelloiOS[9a0b][rb-legacy][57331]): Service exited with abnormal code: 200
        protected override bool IsSignalLine(AppBundleInformation appBundleInfo, string logLine) =>
            logLine.Contains("UIKitApplication:") &&
            logLine.Contains(appBundleInfo.AppName) &&
            logLine.Contains("Service exited with abnormal code");
    }

    public class MacCatalystExitCodeDetector : ExitCodeDetector
    {
        // Example line
        // Feb 18 06:40:16 Admins-Mac-Mini com.apple.xpc.launchd[1] (net.dot.System.Buffers.Tests.15140[59229]): Service exited with abnormal code: 74
        protected override bool IsSignalLine(AppBundleInformation appBundleInfo, string logLine) =>
            logLine.Contains(appBundleInfo.AppName) && logLine.Contains("Service exited with abnormal code");
    }
}
