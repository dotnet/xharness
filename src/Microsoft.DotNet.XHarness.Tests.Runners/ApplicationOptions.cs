// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.Tests.Runners.Core
{

    internal enum XmlMode
    {
        Default = 0,
        Wrapped = 1,
    }

    internal enum XmlVersion
    {
        NUnitV2 = 0,
        NUnitV3 = 1,
        xUnit = 2,
    }

    internal class ApplicationOptions
    {

        static public ApplicationOptions Current = new ApplicationOptions();

        public ApplicationOptions()
        {
            bool b;
            if (bool.TryParse(Environment.GetEnvironmentVariable("NUNIT_AUTOEXIT"), out b))
                TerminateAfterExecution = b;
            if (bool.TryParse(Environment.GetEnvironmentVariable("NUNIT_AUTOSTART"), out b))
                AutoStart = b;
            if (bool.TryParse(Environment.GetEnvironmentVariable("NUNIT_ENABLE_NETWORK"), out b))
                EnableNetwork = b;
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NUNIT_HOSTNAME")))
                HostName = Environment.GetEnvironmentVariable("NUNIT_HOSTNAME");
            int i;
            if (int.TryParse(Environment.GetEnvironmentVariable("NUNIT_HOSTPORT"), out i))
                HostPort = i;
            if (bool.TryParse(Environment.GetEnvironmentVariable("NUNIT_SORTNAMES"), out b))
                SortNames = b;
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NUNIT_TRANSPORT")))
                Transport = Environment.GetEnvironmentVariable("NUNIT_TRANSPORT");
            if (bool.TryParse(Environment.GetEnvironmentVariable("NUNIT_ENABLE_XML_OUTPUT"), out b))
                EnableXml = b;
            var xml_mode = Environment.GetEnvironmentVariable("NUNIT_ENABLE_XML_MODE");
            if (!string.IsNullOrEmpty(xml_mode))
                XmlMode = (XmlMode)Enum.Parse(typeof(XmlMode), xml_mode, true);
            var xml_version = Environment.GetEnvironmentVariable("NUNIT_XML_VERSION");
            if (!string.IsNullOrEmpty(xml_version))
                XmlVersion = (XmlVersion)Enum.Parse(typeof(XmlVersion), xml_version, true);
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NUNIT_LOG_FILE")))
                LogFile = Environment.GetEnvironmentVariable("NUNIT_LOG_FILE");

            var os = new OptionSet() {
                { "autoexit", "Exit application once the test run has completed.", v => TerminateAfterExecution = true },
                { "autostart", "If the app should automatically start running the tests.", v => AutoStart = true },
                { "hostname=", "Comma-separated list of host names or IP address to (try to) connect to", v => HostName = v },
                { "hostport=", "HTTP/TCP port to connect to.", v => HostPort = int.Parse (v) },
                { "enablenetwork", "Enable the network reporter.", v => EnableNetwork = true },
                { "transport=", "Select transport method. Either TCP (default), HTTP or FILE.", v => Transport = v },
                { "enablexml", "Enable the xml reported.", v => EnableXml = false },
                { "xmlmode", "The xml mode.", v => XmlMode = (XmlMode) Enum.Parse (typeof (XmlMode), v, false) },
                { "xmlversion", "The xml version.", v => XmlVersion = (XmlVersion) Enum.Parse (typeof (XmlVersion), v, false) },
                { "logfile=", "A path where output will be saved.", v => LogFile = v },
                { "result=", "The path to be used to store the result", v => ResultFile = v},
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

        private bool EnableNetwork { get; set; }

        /// <summary>
        /// Specify if the Xml returned by the runner should be wrapped by an
        /// extra node so that the human readable test results are added.
        /// </summary>
		public XmlMode XmlMode { get; set; }

        /// <summary>
        /// Specify the version of Xml to be used for the results.
        /// </summary>
        public XmlVersion XmlVersion { get; set; } = XmlVersion.NUnitV2;

        /// <summary>
        /// Return the test results as xml.
        /// </summary>
        public bool EnableXml { get; set; } = true; // always true by default

        /// <summary>
        /// The name of the host that has the device plugged.
        /// </summary>
        public string HostName { get; private set; }

        /// <summary>
        /// The port of the host that has the device plugged.
        /// </summary>
        public int HostPort { get; private set; }

        /// <summary>
        /// Specify if tests should start without human input.
        /// </summary>
        public bool AutoStart { get; set; }

        /// <summary>
        /// Specify is the application should exit once the tests are completed.
        /// </summary>
        public bool TerminateAfterExecution { get; set; }

        /// <summary>
        /// The transport to be used to communicate with the host. The default
        /// value is TCP.
        ///
        /// Supported values are:
        ///
        /// * TCP
        /// * HTTP
        /// </summary>
        public string Transport { get; set; } = "TCP";

        /// <summary>
        /// The path to the file in which logs will be written.
        /// </summary>
        public string LogFile { get; set; }

        /// <summary>
        /// The path to the file in which results will be written.
        /// </summary>
        public string ResultFile { get; set; }

        public bool ShowUseNetworkLogger
        {
            get { return EnableNetwork && !string.IsNullOrWhiteSpace(HostName) && (HostPort > 0 || Transport == "FILE"); }
        }

        /// <summary>
        /// Specify if test results should be sorted by name.
        /// </summary>
        public bool SortNames { get; set; }

    }
}
