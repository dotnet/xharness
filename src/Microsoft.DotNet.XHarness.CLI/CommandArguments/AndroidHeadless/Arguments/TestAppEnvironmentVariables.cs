// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.AndroidHeadless;

/// <summary>
/// Passing these arguments as testing options to a test runner
/// </summary>
internal class TestAppEnvironmentVariables : Argument<Dictionary<string, string>>
{
    public TestAppEnvironmentVariables()
        : base("env=", "Environment variables to pass to the test app, in form key=value", new Dictionary<string, string>())
    {
    }

    public override void Action(string argumentValue)
    {
        var argPair = argumentValue.Split('=', 2);

        if (argPair.Length != 2)
        {
            throw new ArgumentException($"The --env argument expects 'key=value' format. Invalid format found in '{argumentValue}'");
        }

        if (Value.ContainsKey(argPair[0]))
        {
            throw new ArgumentException($"Duplicate env name '{argPair[0]}' found");
        }

        Value.Add(argPair[0].Trim(), argPair[1].Trim());
    }
}
