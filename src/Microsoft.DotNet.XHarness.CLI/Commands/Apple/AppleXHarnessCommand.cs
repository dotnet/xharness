// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.Common;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Apple
{
    internal abstract class AppleXHarnessCommand<TArguments> : XHarnessCommand<TArguments> where TArguments : IXHarnessCommandArguments
    {
        protected AppleXHarnessCommand(string name, bool allowsExtraArgs, string? help = null)
            : base(TargetPlatform.Apple, name, allowsExtraArgs, help)
        {
        }
    }
}
