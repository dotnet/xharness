// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared;

public interface IResultFileHandler
{
    bool IsSimulatorVersionSupported(string osVersion);

    Task<bool> ClearSimulatorResultsAsync(
        RunMode runMode,
        string osVersion,
        string udid,
        string bundleIdentifier,
        CancellationToken cancellationToken);

    Task<bool> CopySimulatorResultsAsync(
        RunMode runMode,
        string osVersion,
        string udid,
        string bundleIdentifier,
        string hostDestinationPath,
        CancellationToken cancellationToken);
}
