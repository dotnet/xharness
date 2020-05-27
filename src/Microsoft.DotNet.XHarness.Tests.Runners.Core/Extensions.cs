// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.Tests.Runners.Core
{
    internal static partial class Extensions
    {
        public static string YesNo(this bool b)
        {
            return b ? "yes" : "no";
        }
    }
}