// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.iOS.Shared
{
    public class AppBundleInformation
    {
        public string AppName { get; }
        public string BundleIdentifier { get; }
        public string AppPath { get; }
        public string Variation { get; set; }
        public string LaunchAppPath { get; }
        public Extension? Extension { get; }

        public AppBundleInformation(string appName, string bundleIdentifier, string appPath, string launchAppPath, Extension? extension)
        {
            AppName = appName;
            BundleIdentifier = bundleIdentifier;
            AppPath = appPath;
            LaunchAppPath = launchAppPath;
            Extension = extension;
        }
    }
}
