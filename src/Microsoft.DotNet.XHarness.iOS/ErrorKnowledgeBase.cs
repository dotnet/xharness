// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;

namespace Microsoft.DotNet.XHarness.iOS
{
    public class ErrorKnowledgeBase : IErrorKnowledgeBase
    {
        static readonly Dictionary<string, string> testErrorMaps = new Dictionary<string, string> {
            ["Failed to communicate with the device"] = "Failed to communicate with the device. Please ensure the cable is properly connected, and try rebooting the device",
        };

        static readonly Dictionary<string, string> buildErrorMaps = new Dictionary<string, string> {
        };

        static readonly Dictionary<string, string> installErrorMaps = new Dictionary<string, string> {
            ["IncorrectArchitecture"] = "IncorrectArchitecture: Failed to find matching device arch for the application.",
        };

        static bool TryFindErrors (ILog? log, Dictionary<string, string> errorMap, out string? failureMessage)
        {
            failureMessage = null;
            if (log == null) {
                return false;
            }

            if (!File.Exists (log.FullPath) || new FileInfo (log.FullPath).Length <= 0)
                return false;

            using var reader = log.GetReader ();
            while (!reader.EndOfStream) {
                string line = reader.ReadLine ();
                if (line == null)
                    continue;
                //go over errors and return true as soon as we find one that matches
                foreach (var error in errorMap.Keys) {
                    if (!line.Contains (error))
                        continue;
                    failureMessage = errorMap [error];
                    return true;
                }
            }

            return false;
        }

        public bool IsKnownBuildIssue (ILog buildLog, out string? knownFailureMessage) =>
            TryFindErrors (buildLog, buildErrorMaps, out knownFailureMessage);

        public bool IsKnownTestIssue (ILog runLog, out string? knownFailureMessage) =>
            TryFindErrors (runLog, testErrorMaps, out knownFailureMessage);

        public bool IsKnownInstallIssue(ILog installLog, [NotNullWhen(true)] out string? knownFailureMessage) =>
            TryFindErrors (installLog, installErrorMaps, out knownFailureMessage);

    }
}
