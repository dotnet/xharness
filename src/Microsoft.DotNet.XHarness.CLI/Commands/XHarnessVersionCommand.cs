using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.Commands;

internal class XHarnessVersionCommand : Command
{
    private static readonly Lazy<FileVersionInfo> s_versionInfo = new(
        () => FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location));

    public XHarnessVersionCommand() : base("version") { }

    public override int Invoke(IEnumerable<string> arguments)
    {
        // Print the name of the tool and the version number unix style
        // Example:
        // Apple clang version 11.0.3 (clang-1103.0.32.29)
        // Target: x86_64-apple-darwin19.4.0
        // InstalledDir: /Applications/Xcode114.app/Contents/Developer/Toolchains/XcodeDefault.xctoolchain/usr/bin
        Console.WriteLine($"XHarness version {XHarnessVersion.ProductVersion} ({XHarnessVersion.OriginalFilename})");
        Console.WriteLine($"InstalledDir: {XHarnessVersion.FileName}");
        return 0;
    }

    public static FileVersionInfo XHarnessVersion => s_versionInfo.Value;
}
