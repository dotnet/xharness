// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Mono.Options;


namespace Microsoft.DotNet.XHarness
{
    public class Program
    {
        private readonly string[] AllowedPlatforms = new string[]
        {
            "android",
            "iphone",
            "applewatch"
        };

        private readonly string[] AllowedActions = new string[]
        {
            "package",
            "test",
        };
        public static void Main(string[] args)
        {
            bool showHelp = false;
            string selectedPlatform = "";
            string selectedAction = "";
            Dictionary<string, string> options = new Dictionary<string, string>();

            OptionSet p = new XHarnessOptionSet() {
                { "p|platform:", v => selectedPlatform = v },
                { "a|action:",   v => selectedAction = v },
                { "o|options:", (k,v) => options.Add (k, v) },
            };

            List<string> extra;
            try
            {
                extra = p.Parse(args);
            }
            catch (OptionException e)
            {
                Console.Write("XHarness: ");
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `XHarness --help' for more information.");
                return;
            }

            if (showHelp)
            {
                p.WriteOptionDescriptions(Console.Out);
                return;
            }

            if (extra.Count > 0)
            {
                Console.WriteLine($"Extra args: '{string.Join(" ", extra.ToArray())}'");
            }
            else
            {
                Console.WriteLine("Arguments accepted (no action currently)");
            }

            Console.WriteLine($"Selected Platform: {selectedPlatform}");
            Console.WriteLine($"Selected Action: {selectedAction}");
            Console.WriteLine("Options:");
            foreach (string key in options.Keys)
            {
                Console.WriteLine($"  {key} = {options[key]}");
            }
        }
    }
}