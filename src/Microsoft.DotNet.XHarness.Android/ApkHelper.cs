using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Microsoft.DotNet.XHarness.Android;

public static class ApkHelper
{
    public static List<string> GetApkSupportedArchitectures(string apkPath)
    {
        if (string.IsNullOrEmpty(apkPath))
        {
            throw new ArgumentException(Microsoft.DotNet.XHarness.Common.Resources.Strings.Android_ApkHelper_SupplyApkPath);
        }
        if (!File.Exists(apkPath))
        {
            throw new FileNotFoundException(string.Format(Microsoft.DotNet.XHarness.Common.Resources.Strings.Android_ApkHelper_InvalidApkPath, apkPath), apkPath);
        }
        if (!Path.GetExtension(apkPath).Equals(".apk", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(Microsoft.DotNet.XHarness.Common.Resources.Strings.Android_ApkHelper_OnlyApkFiles);
        }

        using (ZipArchive archive = ZipFile.Open(apkPath, ZipArchiveMode.Read))
        {
            // Enumerate all folders under /lib inside the zip
            var allLibFolders = archive.Entries.Where(e => e.FullName.StartsWith("lib/"))
                                               .Select(e => e.FullName[4..e.FullName.IndexOf('/', 4)])
                                               .Distinct().ToList();

            return allLibFolders;
        }
    }
}
