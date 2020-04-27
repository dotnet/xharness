namespace Microsoft.DotNet.XHarness.iOS.Shared.Execution.Mlaunch
{
    public static class EnviromentVariables
    {

        /// <summary>
        /// Env var that will tell the runner to start the execution of the tests automatically.
        /// </summary>
        public static string AutoStart = "NUNIT_AUTOSTART";

        /// <summary>
        /// Env car that will tell the test application to exit once all the test have been ran.
        /// </summary>
        public static string AutoExit = "NUNIT_AUTOEXIT";

        /// <summary>
        /// Env var that will tell the test application to enable network on the device (iOS).
        /// </summary>
        public static string EnableNetwork = "NUNIT_ENABLE_NETWORK";

        /// <summary>
        /// Env var that will tell the test application to ignore those tests that required a permission to
        /// execute on the device.
        /// </summary>
        public static string DisableSystemPermissionTests = "DISABLE_SYSTEM_PERMISSION_TESTS";

        /// <summary>
        /// Env var that provide the test application the name of the host.
        /// </summary>
        public static string HostName = "NUNIT_HOSTNAME";

        /// <summary>
        /// Env var that provides the test application with the transport to use to communicate with the host.
        /// </summary>
        public static string Transport = "NUNIT_TRANSPORT";

        /// <summary>
        /// Env var that provides the test application with the path to be used to store the execution logs.
        /// </summary>
        public static string LogFilePath = "NUNIT_LOG_FILE";

        /// <summary>
        /// Env var that provide the test application the port to be used to connect with the host.
        /// </summary>
        public static string HostPort = "NUNIT_HOSTPORT";

        /// <summary>
        /// Env var used to notify the test application that the communication will be done using a tcp tunnel
        /// over the usb cable.
        /// </summary>
        public static string UseTcpTunnel = "USE_TCP_TUNNEL";

        /// <summary>
        /// Env var used to notify the test application that the output is expected to be xml.
        /// </summary>
        public static string EnableXmlOutput = "NUNIT_ENABLE_XML_OUTPUT";

        /// <summary>
        /// Env var used to notify the test application the xml mode to be used.
        /// </summary>
        public static string XmlMode = "NUNIT_ENABLE_XML_MODE";

        /// <summary>
        /// Env var used to pass the format of the xml used for results.
        /// </summary>
        public static string XmlVersion = "NUNIT_XML_VERSION";

        /// <summary>
        /// Env var used to notify the test application that the test should be sorted by name.
        /// </summary>
        public static string SortByName = "NUNIT_SORTNAMES";
    }
}
