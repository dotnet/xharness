// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple
{
    /// <summary>
    /// Path to the app bundle.
    /// </summary>
    internal class AppPathArgument : PathArgument
    {
        public AppPathArgument() : base("app|a=", "Path to an already-packaged app")
        {
        }

        public override void Validate()
        {
            if (string.IsNullOrEmpty(Value))
            {
                throw new ArgumentException("You must provide a path to the application");
            }
        }
    }
}
