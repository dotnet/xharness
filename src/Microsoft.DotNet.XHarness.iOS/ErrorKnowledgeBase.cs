// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
using System;
using System.IO;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;

namespace Microsoft.DotNet.XHarness.iOS
{
    public class ErrorKnowledgeBase : IErrorKnowledgeBase
    {
        const string IncorrectArchPrefix = "IncorrectArchitecture";

        public bool IsKnownInstallIssue(ILog installLog, out string? knownFailureMessage)
        {
            knownFailureMessage = null;
            if (installLog == null)
            {
                return false;
            }

            if (File.Exists(installLog.FullPath) && new FileInfo(installLog.FullPath).Length > 0)
            {
                using StreamReader reader = installLog.GetReader();
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (line == null)
                    {
                        continue;
                    }

                    var index = line.IndexOf("IncorrectArchitecture", StringComparison.Ordinal);
                    if (index >= 0)
                    {
                        // add the information from the line, which is good enough
                        knownFailureMessage = line.Substring(index); // remove the timestamp if any
                        return true;
                    }
                }
            }

            return false;
        }

        public bool IsKnownBuildIssue(ILog buildLog, out string? knownFailureMessage)
        {
            knownFailureMessage = null;
            return false;
        }

        public bool IsKnownTestIssue(ILog runLog, out string? knownFailureMessage)
        {
            knownFailureMessage = null;
            return false;
        }
    }
}
