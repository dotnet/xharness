// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;

internal class ApiVersionArgument : RepeatableArgument
{
    public ApiVersionArgument()
        : base("api-version=|api=", "Target a device/emulator with given Android API version (level)")
    {
    }

    /// <summary>
    /// Gets the API versions as integers.
    /// </summary>
    public IEnumerable<int> ApiVersions => Value.Select(v => int.Parse(v));

    /// <summary>
    /// Gets the first API version as a nullable int for backward compatibility.
    /// Returns null if no API version is specified.
    /// </summary>
    public int? FirstApiVersion => Value.Any() ? int.Parse(Value.First()) : null;

    public override void Validate()
    {
        foreach (var apiVersion in Value)
        {
            if (!int.TryParse(apiVersion, out var level))
            {
                throw new ArgumentException($"API version '{apiVersion}' must be an integer");
            }

            if (level < 16 || level > 35)
            {
                throw new ArgumentException($"API version {level} is not supported. Supported range is 16-35");
            }
        }
    }
}
