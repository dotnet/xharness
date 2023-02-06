// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm;
using System;

internal class LocaleArgument : StringArgument
{
    public LocaleArgument(string defaultValue)
        : base("locale=", $"Sets LANG environment variable, default value {defaultValue}")
    {}
}
