// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.CLI.Commands;

namespace Microsoft.DotNet.XHarness.CLI.Android
{
    internal abstract class AndroidXHarnessCommand<TArguments> : XHarnessCommand<TArguments> where TArguments : IXHarnessCommandArguments
    {
        protected AndroidXHarnessCommand(string name, bool allowsExtraArgs, string? help = null) : base(TargetPlatform.Android, name, allowsExtraArgs, help)
        {
        }
    }
}
