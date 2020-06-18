// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;

namespace Microsoft.DotNet.XHarness.iOS
{
    public class ErrorKnowledgeBase : IErrorKnowledgeBase
    {
        private static readonly Dictionary<string, (string HumanMessage, string? IssueLink)> s_testErrorMaps = new Dictionary<string, (string HumanMessage, string? IssueLink)>
        {
            ["Failed to communicate with the device"] = // Known issue but not a failure.
                (HumanMessage: "Failed to communicate with the device. Please ensure the cable is properly connected, and try rebooting the device", IssueLink: (string?)null)

        };

        private static readonly Dictionary<string, (string HumanMessage, string? IssueLink)> s_buildErrorMaps = new Dictionary<string, (string HumanMessage, string? IssueLink)>();

        private static readonly Dictionary<string, (string HumanMessage, string? IssueLink)> s_installErrorMaps = new Dictionary<string, (string HumanMessage, string? IssueLink)>
        {
            ["IncorrectArchitecture"] =
                (HumanMessage: "IncorrectArchitecture: Failed to find matching device arch for the application.", IssueLink: (string?)null) // known failure, but not an issue

        };

        public bool IsKnownBuildIssue(IFileBackedLog buildLog,
                                      [NotNullWhen(true)]
                                      out (string HumanMessage, string? IssueLink)? knownFailureMessage)
            => TryFindErrors(buildLog, s_buildErrorMaps, out knownFailureMessage);

        public bool IsKnownTestIssue(IFileBackedLog runLog,
                                    [NotNullWhen(true)]
                                    out (string HumanMessage, string? IssueLink)? knownFailureMessage)
            => TryFindErrors(runLog, s_testErrorMaps, out knownFailureMessage);

        public bool IsKnownInstallIssue(IFileBackedLog installLog,
                                        [NotNullWhen(true)]
                                        out (string HumanMessage, string? IssueLink)? knownFailureMessage)
            => TryFindErrors(installLog, s_installErrorMaps, out knownFailureMessage);

        private static bool TryFindErrors(IFileBackedLog log, Dictionary<string, (string HumanMessage, string? IssueLink)> errorMap,
            [NotNullWhen(true)] out (string HumanMessage, string? IssueLink)? failureMessage)
        {
            failureMessage = null;
            if (log == null)
            {
                return false;
            }

            if (!File.Exists(log.FullPath) || new FileInfo(log.FullPath).Length <= 0)
            {
                return false;
            }

            using var reader = log.GetReader();
            while (!reader.EndOfStream)
            {
                string line = reader.ReadLine();
                if (line == null)
                {
                    continue;
                }

                //go over errors and return true as soon as we find one that matches
                foreach (var error in errorMap.Keys)
                {
                    if (!line.Contains(error, StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }

                    failureMessage = errorMap[error];
                    return true;
                }
            }

            return false;
        }
    }
}
