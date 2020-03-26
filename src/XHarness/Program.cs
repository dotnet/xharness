// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Mono.Options;
using System;
using XHarness.Android;
using XHarness.iOS;

namespace Microsoft.DotNet.XHarness
{
	public class Program
	{
		public static int Main(string[] args)
		{
			Console.WriteLine(string.Join(' ', args));

			// Root command: will use the platform specific commands to perform the appropriate action.
			var commands = new CommandSet("xharness");

			// Add per-platform CommandSets - If adding  a new supported set, that goes here. 
			commands.Add(new iOSCommandSet());
			commands.Add(new AndroidCommandSet());

			// add shared commands, for example, help and so on. --version, --help, --verbosity and so on
			commands.Add(new XHarnessHelpCommand());

			return commands.Run(args);
		}
	}
}