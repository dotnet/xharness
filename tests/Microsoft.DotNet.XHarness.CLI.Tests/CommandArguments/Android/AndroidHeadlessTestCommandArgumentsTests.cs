// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.AndroidHeadless;
using Xunit;

namespace Microsoft.DotNet.XHarness.CLI.Tests.CommandArguments.Android;

public class AndroidHeadlessTestCommandArgumentsTests
{
    [Fact]
    public void AndroidHeadlessTestCommandArguments_AllowsSingleApiVersion()
    {
        // Arrange
        var arguments = new AndroidHeadlessTestCommandArguments();
        arguments.TestPath.Action("/tmp/test");
        arguments.RuntimePath.Action("/tmp/runtime");
        arguments.TestAssembly.Action("test.dll");
        arguments.TestScript.Action("test.sh");
        arguments.OutputDirectory.Action("/tmp/output");
        arguments.ApiVersion.Action("28");

        // Act & Assert - should not throw
        arguments.Validate();
        
        Assert.Single(arguments.ApiVersion.Value);
        Assert.Equal(28, arguments.ApiVersion.ApiVersions.First());
    }

    [Fact]
    public void AndroidHeadlessTestCommandArguments_AllowsMultipleApiVersions()
    {
        // Arrange
        var arguments = new AndroidHeadlessTestCommandArguments();
        arguments.TestPath.Action("/tmp/test");
        arguments.RuntimePath.Action("/tmp/runtime");
        arguments.TestAssembly.Action("test.dll");
        arguments.TestScript.Action("test.sh");
        arguments.OutputDirectory.Action("/tmp/output");
        arguments.ApiVersion.Action("28");
        arguments.ApiVersion.Action("29");
        arguments.ApiVersion.Action("30");

        // Act & Assert - should not throw
        arguments.Validate();
        
        Assert.Equal(3, arguments.ApiVersion.Value.Count());
        Assert.Equal(new[] { 28, 29, 30 }, arguments.ApiVersion.ApiVersions);
    }

    [Fact]
    public void AndroidHeadlessTestCommandArguments_AllowsNoApiVersion()
    {
        // Arrange
        var arguments = new AndroidHeadlessTestCommandArguments();
        arguments.TestPath.Action("/tmp/test");
        arguments.RuntimePath.Action("/tmp/runtime");
        arguments.TestAssembly.Action("test.dll");
        arguments.TestScript.Action("test.sh");
        arguments.OutputDirectory.Action("/tmp/output");

        // Act & Assert - should not throw
        arguments.Validate();
        
        Assert.Empty(arguments.ApiVersion.Value);
    }
}