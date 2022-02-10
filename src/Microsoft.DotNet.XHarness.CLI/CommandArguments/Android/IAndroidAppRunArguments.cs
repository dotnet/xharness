// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;

internal interface IAndroidAppRunArguments
{
    public PackageNameArgument PackageName { get; }
    public OutputDirectoryArgument OutputDirectory { get; }
    public TimeoutArgument Timeout { get; }
    public LaunchTimeoutArgument LaunchTimeout { get; }
    public DeviceIdArgument DeviceId { get; }
    public DeviceArchitectureArgument DeviceArchitecture { get; }
    public ApiVersionArgument ApiVersion { get; }
}
