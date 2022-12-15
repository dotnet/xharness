// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.DotNet.XHarness.Common.Utilities;

public static class FileUtils
{
    public static string FindFileInPath(string engineBinary)
    {
        if (File.Exists(engineBinary) || Path.IsPathRooted(engineBinary))
            return engineBinary;

        var path = Environment.GetEnvironmentVariable("PATH");

        if (path == null)
            return engineBinary;

        foreach (var folder in path.Split(Path.PathSeparator))
        {
            var fullPath = Path.Combine(folder, engineBinary);
            if (File.Exists(fullPath))
                return fullPath;
        }

        return engineBinary;
    }
}
