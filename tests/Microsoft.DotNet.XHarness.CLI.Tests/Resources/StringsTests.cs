// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.CLI.Resources;
using Xunit;

namespace Microsoft.DotNet.XHarness.CLI.Tests.Resources;

public class StringsTests
{
    [Fact]
    public void ResourcesCanBeLoaded()
    {
        Assert.NotNull(Strings.Apple_Test_Description);
        Assert.NotEmpty(Strings.Apple_Test_Description);

        Assert.NotNull(Strings.Apple_Test_Usage);
        Assert.NotEmpty(Strings.Apple_Test_Usage);
    }
}
