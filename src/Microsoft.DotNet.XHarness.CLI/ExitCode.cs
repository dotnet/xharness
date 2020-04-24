// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI
{
    /// <summary>
    ///  Exit codes to use for common failure reasons; if you add a new exit code, add it here and use the enum.
    /// </summary>
    internal enum ExitCode
    {
        SUCCESS = 0,
        PACKAGE_NOT_FOUND = -42,
        PACKAGE_INSTALLATION_FAILURE = -43,
        GENERAL_FAILURE = -44,
        FAILED_TO_GET_BUNDLE_INFO = -45,
        APP_CRASH = -46,
        DEVICE_NOT_FOUND = -47,
    }
}
