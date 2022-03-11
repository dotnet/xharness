// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.AndroidHeadless;

internal interface IAndroidHeadlessAppRunArguments
{
    TestAppPathArgument TestAppPath { get; }
    TestAppCommandArgument TestAppCommand { get; }
    OutputDirectoryArgument OutputDirectory { get; }
    TimeoutArgument Timeout { get; }
    LaunchTimeoutArgument LaunchTimeout { get; }
    DeviceIdArgument DeviceId { get; }
    ApiVersionArgument ApiVersion { get; }
}
