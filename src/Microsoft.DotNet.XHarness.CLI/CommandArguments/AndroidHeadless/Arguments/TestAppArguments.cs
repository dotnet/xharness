// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.AndroidHeadless;

/// <summary>
/// Passing these arguments as testing options to a test runner
/// </summary>
internal class TestAppArguments : Argument<List<string>>
{
    public TestAppArguments()
        : base("arg=", "Argument to pass to the command", new List<string>())
    {
    }

    public override void Action(string argumentValue)
    {
        Value.Add(argumentValue);
    }
}
