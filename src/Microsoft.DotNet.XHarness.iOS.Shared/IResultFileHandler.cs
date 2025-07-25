// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared;

public interface IResultFileHandler
{
    /// <summary>
    /// Determines whether the result file handler supports the given OS version and simulator status.
    /// </summary>
    bool IsVersionSupported(string osVersion, bool isSimulator);

    /// <summary>
    /// Copy the XML results file from the app container (simulator or device) to the host path.
    /// </summary>
    Task<bool> CopyResultsAsync(
        RunMode runMode,
        bool isSimulator,
        string osVersion,
        string udid,
        string bundleIdentifier,
        string hostDestinationPath,
        CancellationToken token);
}
