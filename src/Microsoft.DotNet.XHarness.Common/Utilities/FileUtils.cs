// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.XHarness.Common.Utilities;

public static class FileUtils
{
    private static readonly string[] s_extensions = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                                                        ? new[] { ".exe", ".cmd", ".bat" }
                                                        : new[] { "" };

    public static (string fullPath, string? errorMessage) FindExecutableInPATH(string filename)
    {
        if (File.Exists(filename) || Path.IsPathRooted(filename))
            return (filename, null);

        var path = Environment.GetEnvironmentVariable("PATH");

        if (path == null)
            return (filename, null);

        List<string> filenamesTried = new(s_extensions.Length);
        foreach (string extn in s_extensions)
        {
            string filenameWithExtn = filename + extn;
            filenamesTried.Add(filenameWithExtn);
            foreach (var folder in path.Split(Path.PathSeparator))
            {
                var fullPath = Path.Combine(folder, filenameWithExtn);
                if (File.Exists(fullPath))
                    return (fullPath, null);
            }
        }

        // Could not find the path
        return (filename, $"Tried to look for {string.Join(", ", filenamesTried)} .");
    }
}
