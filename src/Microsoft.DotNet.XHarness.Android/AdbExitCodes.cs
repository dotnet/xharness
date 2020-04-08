// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.Android
{
    /// <summary>
    ///  Exit codes we monitor from ADB commands
    /// </summary>
    public enum AdbExitCodes
    {
        ADB_UNINSTALL_APP_NOT_ON_DEVICE = 255,
        INSTRUMENTATION_SUCCESS = -1,
        SUCCESS = 0,
    }
}
