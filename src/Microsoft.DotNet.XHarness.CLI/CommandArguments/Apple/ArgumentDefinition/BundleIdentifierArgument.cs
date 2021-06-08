// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple
{
    internal class BundleIdentifierArgument : StringArgument
    {
        public BundleIdentifierArgument() : base("app|a=", "Bundle identifier of the app that should be uninstalled")
        {
        }

        public override void Validate()
        {
            if (string.IsNullOrEmpty(Value))
            {
                throw new ArgumentNullException("You must provide bundle identifier of the app");
            }
        }
    }
}
