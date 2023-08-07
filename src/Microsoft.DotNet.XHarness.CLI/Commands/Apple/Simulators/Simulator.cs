// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Apple.Simulators;

internal class Simulator
{
    public string Name { get; }
    public string Identifier { get; }
    public string Version { get; }
    public string Source { get; }
    public string InstallPrefix { get; }
    public long FileSize { get; }
    public bool IsDmgFormat { get; }

    public Simulator(string name, string identifier, string version, string source, string installPrefix, long fileSize)
    {
        Name = name;
        Identifier = identifier;
        Version = version;
        Source = source;
        InstallPrefix = installPrefix;
        FileSize = fileSize;
        IsDmgFormat = Identifier.StartsWith("com.apple.dmg.", StringComparison.OrdinalIgnoreCase);
    }
}
