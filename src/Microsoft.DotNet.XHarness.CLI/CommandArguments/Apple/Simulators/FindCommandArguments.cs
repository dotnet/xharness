﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple.Simulators;

internal class FindCommandArguments : SimulatorsCommandArguments
{
    protected override IEnumerable<Argument> GetAdditionalArguments() => Enumerable.Empty<Argument>();
}
