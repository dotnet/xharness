// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.iOS.Shared.TestImporter
{
    /// <summary>
    /// Represents the supported platforms to which we can create projects.
    /// </summary>
    public enum Platform
    {
        iOS,
        WatchOS,
        TvOS,
        MacOSFull,
        MacOSModern,
    }

    /// <summary>
    /// Represents the different types of wathcOS apps.
    /// </summary>
    public enum WatchAppType
    {
        App,
        Extension
    }
}
