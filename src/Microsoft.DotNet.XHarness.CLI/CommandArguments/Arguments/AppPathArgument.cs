// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.CLI.Resources;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments;

/// <summary>
/// Path to the app bundle.
/// </summary>
internal class AppPathArgument : RequiredPathArgument
{
    public AppPathArgument() : base("app|a=", Strings.Arg_AppPath_Description)
    {
    }
}

/// <summary>
/// Path to the app bundle.
/// </summary>
internal class OptionalAppPathArgument : PathArgument
{
    public OptionalAppPathArgument() : base("app|a=", Strings.Arg_AppPath_Description, false)
    {
    }
}
