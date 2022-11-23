// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.Common;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasi;

internal class SymbolicatorArgument : TypeFromAssemblyArgument<WasiSymbolicatorBase>
{
    public SymbolicatorArgument()
        : base(
              "symbolicator=",
              $"<path>,<typeName> to assembly, and type which contains the wasi symbolicator",
              repeatable: false)
    {
    }
}
