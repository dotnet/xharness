﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm
{
    internal class JavaScriptEngineArguments : RepetableArgument
    {
        public JavaScriptEngineArguments()
            : base("engine-arg=", "Argument to pass to the JavaScript engine. Can be used more than once.")
        {
        }
    }
}
