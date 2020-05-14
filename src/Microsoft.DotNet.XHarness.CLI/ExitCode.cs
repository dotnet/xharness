// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI
{
    /// <summary>
    /// Exit codes to use for common failure reasons; if you add a new exit code, add it here and use the enum.
    /// The first part conforms with xUnit: https://xunit.net/docs/getting-started/netfx/visual-studio
    /// </summary>
    internal enum ExitCode
    {
        /// <summary>
        /// The tests ran successfully
        /// </summary>
        SUCCESS = 0,

        /// <summary>
        /// One or more of the tests failed
        /// </summary>
        TESTS_FAILED = 1,

        /// <summary>
        /// The help page was shown
        /// Either because it was requested, or because the user did not provide any command line arguments
        /// </summary>
        HELP_SHOWN = 2,

        /// <summary>
        /// There was a problem with one of the command line options
        /// </summary>
        INVALID_ARGUMENTS = 3,

        /// <summary>
        /// There was a problem loading one or more of the test packages
        /// </summary>
        PACKAGE_NOT_FOUND = 4,

        #region General failures

        TIMED_OUT = 1000,
        GENERAL_FAILURE = 1001,

        #endregion

        #region Running the test package

        PACKAGE_INSTALLATION_FAILURE = 1002,
        FAILED_TO_GET_BUNDLE_INFO = 1003,
        APP_CRASH = 1004,
        DEVICE_NOT_FOUND = 1005,
        RETURN_CODE_NOT_SET = 1006,

        #endregion

        #region Packaging the bundle

        PACKAGE_BUNDLING_FAILURE_NUGET_RESTORE = 1101,
        PACKAGE_BUNDLING_FAILURE_BUILD = 1102,

        #endregion
    }
}
