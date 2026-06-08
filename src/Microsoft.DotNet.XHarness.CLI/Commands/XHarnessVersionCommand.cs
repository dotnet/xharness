using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.Commands;

internal class XHarnessVersionCommand : Command
{
    public XHarnessVersionCommand() : base("version") { }

    public override int Invoke(IEnumerable<string> arguments)
    {
        var version = GetProductVersion();
        var installedDir = GetInstalledDir();

        if (!arguments.Contains("-v"))
        {
            Console.WriteLine(version);
            return 0;
        }

        // Print the name of the tool and the version number unix style
        // Example:
        // Apple clang version 11.0.3 (clang-1103.0.32.29)
        // Target: x86_64-apple-darwin19.4.0
        // InstalledDir: /Applications/Xcode114.app/Contents/Developer/Toolchains/XcodeDefault.xctoolchain/usr/bin
        Console.WriteLine($"XHarness version {version}");
        Console.WriteLine($"InstalledDir: {installedDir}");
        return 0;
    }

    /// <summary>
    /// Returns the product version string. Prefers
    /// <see cref="AssemblyInformationalVersionAttribute"/> (set by Arcade at build
    /// time and embedded in the managed metadata bundled inside both JIT and AOT
    /// builds), then <see cref="AssemblyFileVersionAttribute"/>, and finally
    /// <see cref="FileVersionInfo.ProductVersion"/> as a last resort for cases
    /// where the assembly is loaded from disk (legacy / global-tool path).
    /// </summary>
    public static string GetProductVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();

        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(informational))
        {
            return informational;
        }

        var fileVersionAttr = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        if (!string.IsNullOrEmpty(fileVersionAttr))
        {
            return fileVersionAttr;
        }

        var location = GetExecutableLocation();
        if (!string.IsNullOrEmpty(location) && File.Exists(location))
        {
            var fvi = FileVersionInfo.GetVersionInfo(location);
            if (!string.IsNullOrEmpty(fvi.ProductVersion))
            {
                return fvi.ProductVersion!;
            }
        }

        return "0.0.0";
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
        "SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file",
        Justification = "Assembly.Location returns the empty string under single-file/AOT; that case is explicitly handled by falling back to Environment.ProcessPath.")]
    private static string? GetExecutableLocation()
    {
        var location = Assembly.GetExecutingAssembly().Location;
        if (!string.IsNullOrEmpty(location))
        {
            return location;
        }

        return Environment.ProcessPath;
    }

    private static string GetInstalledDir()
    {
        var location = GetExecutableLocation();
        if (string.IsNullOrEmpty(location))
        {
            return string.Empty;
        }

        return Path.GetDirectoryName(location) ?? string.Empty;
    }
}
