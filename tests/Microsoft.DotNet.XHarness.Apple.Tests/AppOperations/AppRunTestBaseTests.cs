// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Xunit;

namespace Microsoft.DotNet.XHarness.Apple.Tests.AppOperations;

public class AppRunTestBaseTests
{
    [Fact]
    public void InstancesHaveIsolatedOutputDirectories()
    {
        using var second = new TestAppRun();
        string firstOutputPath;

        using (var first = new TestAppRun())
        {
            firstOutputPath = first.OutputPath;
            Assert.NotEqual(first.OutputPath, second.OutputPath);
            Assert.True(Directory.Exists(first.OutputPath));
            Assert.True(Directory.Exists(second.OutputPath));
        }

        Assert.False(Directory.Exists(firstOutputPath));
        Assert.True(Directory.Exists(second.OutputPath));
    }

    private sealed class TestAppRun : AppRunTestBase
    {
        public string OutputPath => _outputPath;
    }
}
