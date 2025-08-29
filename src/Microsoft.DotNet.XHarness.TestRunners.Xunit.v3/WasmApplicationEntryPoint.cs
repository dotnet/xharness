// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.TestRunners.Common;

#nullable enable
namespace Microsoft.DotNet.XHarness.TestRunners.Xunit;

public abstract class WasmApplicationEntryPoint : WasmApplicationEntryPointBase
{
    protected override bool IsXunit => true;

    protected override TestRunner GetTestRunner(LogWriter logWriter)
    {
        // WASM support for xunit v3 is not yet implemented
        throw new NotSupportedException("WASM support for xunit v3 is not yet available. Please use the xunit v2 package for WASM scenarios.");
    }
}