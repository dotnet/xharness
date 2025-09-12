// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;

/// <summary>
/// Argument for specifying multiple Android API levels for testing.
/// Allows targeting multiple emulators with different API levels in a single test run.
/// </summary>
internal class ApiLevelsArgument : RepeatableArgument
{
    public ApiLevelsArgument()
        : base("api-levels=", 
            "Run tests on devices/emulators with specified Android API version levels. Can be specified multiple times for multiple API levels")
    {
    }

    /// <summary>
    /// Gets the API levels as integers.
    /// </summary>
    public IEnumerable<int> ApiLevels => Value.Select(v => int.Parse(v));

    public override void Validate()
    {
        foreach (var apiLevel in Value)
        {
            if (!int.TryParse(apiLevel, out var level))
            {
                throw new ArgumentException($"API level '{apiLevel}' must be an integer");
            }

            if (level < 16 || level > 35)
            {
                throw new ArgumentException($"API level {level} is not supported. Supported range is 16-35");
            }
        }
    }
}