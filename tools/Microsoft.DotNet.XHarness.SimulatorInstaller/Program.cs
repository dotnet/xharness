// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.SimulatorInstaller.Commands;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.SimulatorInstaller
{
    /// <summary>
    /// This command line tool allows management of Xcode iOS/WatchOS/tvOS Simulators on MacOS.
    /// It is used for automated update of OSX servers.
    /// Originally taken from: https://github.com/xamarin/xamarin-macios/blob/master/tools/siminstaller/Program.cs
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            var commands = new CommandSet("simulator-installer")
            {
                new InstallCommand(),
                new ListCommand(),
                new FindCommand(),
                new HelpCommand(),
            };

            return commands.Run(args);
        }
    }
}
