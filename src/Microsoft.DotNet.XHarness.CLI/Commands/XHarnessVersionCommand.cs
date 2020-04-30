using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.Commands
{
    internal class XHarnessVersionCommand : Command
    {
        public XHarnessVersionCommand() : base("version") { }

        public override int Invoke(IEnumerable<string> arguments)
        {
            // ignore arguments, print the name of the tool and the version number unix style, for example
            // man --version
            // man, version 1.6g
            // gcc --version
            // Apple clang version 11.0.3 (clang-1103.0.32.29)
            // Target: x86_64-apple-darwin19.4.0
            // Thread model: posix
            // InstalledDir: /Applications/Xcode114.app/Contents/Developer/Toolchains/XcodeDefault.xctoolchain/usr/bin
            var assembly = Assembly.GetExecutingAssembly();
            var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            Console.WriteLine($"xharness version {fvi.ProductVersion} ({fvi.OriginalFilename})");
            Console.WriteLine($"InstalledDir: {fvi.FileName}");
            return 0;
        }
    }
}
