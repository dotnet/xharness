// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.TestRunners.Common
{
    internal enum XmlMode
    {
        Default = 0,
        Wrapped = 1,
    }

    internal class ApplicationOptions
    {
        public static ApplicationOptions Current = new ApplicationOptions();
        private readonly List<string> _singleMethodFilters = new List<string>();
        private readonly List<string> _classMethodFilters = new List<string>();

        public ApplicationOptions()
        {
            if (bool.TryParse(Environment.GetEnvironmentVariable(EnviromentVariables.AutoExit), out bool b))
            {
                TerminateAfterExecution = b;
            }

            if (bool.TryParse(Environment.GetEnvironmentVariable(EnviromentVariables.AutoStart), out b))
            {
                AutoStart = b;
            }

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnviromentVariables.HostName)))
            {
                HostName = Environment.GetEnvironmentVariable(EnviromentVariables.HostName);
            }

            if (int.TryParse(Environment.GetEnvironmentVariable(EnviromentVariables.HostPort), out int i))
            {
                HostPort = i;
            }

            if (bool.TryParse(Environment.GetEnvironmentVariable(EnviromentVariables.EnableXmlOutput), out b))
            {
                EnableXml = b;
            }

            var xml_version = Environment.GetEnvironmentVariable(EnviromentVariables.XmlVersion);
            if (!string.IsNullOrEmpty(xml_version))
            {
                XmlVersion = (XmlResultJargon)Enum.Parse(typeof(XmlResultJargon), xml_version, true);
            }

            if (bool.TryParse(Environment.GetEnvironmentVariable(EnviromentVariables.RunAllTestsByDefault), out b))
            {
                RunAllTestsByDefault = b;
            }

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnviromentVariables.SkippedMethods)))
            {
                var methods = Environment.GetEnvironmentVariable(EnviromentVariables.SkippedMethods);
                _singleMethodFilters.AddRange(methods.Split(','));
            }
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnviromentVariables.SkippedClasses)))
            {
                var classes = Environment.GetEnvironmentVariable(EnviromentVariables.SkippedClasses);
                _classMethodFilters.AddRange(classes.Split(','));
            }

            var os = new OptionSet() {
                { "autoexit", "Exit application once the test run has completed", v => TerminateAfterExecution = true },
                { "autostart", "If the app should automatically start running the tests", v => AutoStart = true },
                { "hostname=", "Comma-separated list of host names or IP address to (try to) connect to", v => HostName = v },
                { "hostport=", "HTTP/TCP port to connect to", v => HostPort = int.Parse (v) },
                { "enablexml", "Enable the xml reported", v => EnableXml = false },
                { "xmlversion", "The XML format", v => XmlVersion = (XmlResultJargon) Enum.Parse (typeof (XmlResultJargon), v, false) },
                { "run-all-tests:", "Run all the tests found in the assembly, defaults to true", v =>
                    {
                        // if cannot parse, use default
                        if (bool.TryParse(v, out var runAll))
                        {
                            RunAllTestsByDefault = runAll;
                        }
                    }
                },
                {
                    "method|m=",
                    "Method to be ran in the test application. When this parameter is used only the " +
                    "tests that have been provided by the '--method' and '--class' arguments will be ran. " +
                    "All other test will be ignored. Can be used more than once.",
                    v => _singleMethodFilters.Add(v)
                },
                {
                    "class|c=",
                    "Method to be ran in the test application. When this parameter is used only the " +
                    "tests that have been provided by the '--method' and '--class' arguments will be ran. " +
                    "All other test will be ignored. Can be used more than once.",
                    v => _classMethodFilters.Add(v)
                }
            };

            try
            {
                os.Parse(Environment.GetCommandLineArgs());
            }
            catch (OptionException oe)
            {
                Console.WriteLine("{0} for options '{1}'", oe.Message, oe.OptionName);
            }
        }

        /// <summary>
        /// Specify if tests should start without human input.
        /// </summary>
        public bool AutoStart { get; set; }

        /// <summary>
        /// Specify the version of Xml to be used for the results.
        /// </summary>
        public XmlResultJargon XmlVersion { get; private set; } = XmlResultJargon.xUnit;

        /// <summary>
        /// Return the test results as xml.
        /// </summary>
        public bool EnableXml { get; private set; } = true; // always true by default

        /// <summary>
        /// The name of the host that has the device plugged.
        /// </summary>
        public string HostName { get; private set; }

        /// <summary>
        /// The port of the host that has the device plugged.
        /// </summary>
        public int HostPort { get; private set; }

        /// <summary>
        /// Specify is the application should exit once the tests are completed.
        /// </summary>
        public bool TerminateAfterExecution { get; private set; }

        /// <summary>
        /// Specify if all the tests should be run by default or not. Defaults to true.
        /// </summary>
        public bool RunAllTestsByDefault { get; private set; } = true;

        /// <summary>
        /// Specify the methods to be ran in the app.
        /// </summary>
        public IEnumerable<string> SingleMethodFilters => _singleMethodFilters;

        /// <summary>
        /// Specify the test classes to be ran in the app.
        /// </summary>
        public IEnumerable<string> ClassMethodFilters => _classMethodFilters;
    }
}
