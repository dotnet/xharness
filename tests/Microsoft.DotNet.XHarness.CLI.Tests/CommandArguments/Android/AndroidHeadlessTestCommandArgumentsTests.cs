// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.AndroidHeadless;
using Xunit;

namespace Microsoft.DotNet.XHarness.CLI.Tests.CommandArguments.Android;

public class AndroidHeadlessTestCommandArgumentsTests
{
    [Fact]
    public void AndroidHeadlessTestCommandArguments_ValidatesApiVersionAndApiLevelsNotBothSpecified()
    {
        // Arrange
        var arguments = new AndroidHeadlessTestCommandArguments();
        arguments.TestPath.Action("/tmp/test");
        arguments.RuntimePath.Action("/tmp/runtime");
        arguments.TestAssembly.Action("test.dll");
        arguments.TestScript.Action("test.sh");
        arguments.OutputDirectory.Action("/tmp/output");
        arguments.ApiVersion.Action("28");
        arguments.ApiLevels.Action("29");
        arguments.ApiLevels.Action("30");

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => arguments.Validate());
        Assert.Contains("Cannot specify both --api-version and --api-levels", exception.Message);
    }

    [Fact]
    public void AndroidHeadlessTestCommandArguments_AllowsOnlyApiVersion()
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
    }

    [Fact]
    public void AndroidHeadlessTestCommandArguments_AllowsOnlyApiLevels()
    {
        // Arrange
        var arguments = new AndroidHeadlessTestCommandArguments();
        arguments.TestPath.Action("/tmp/test");
        arguments.RuntimePath.Action("/tmp/runtime");
        arguments.TestAssembly.Action("test.dll");
        arguments.TestScript.Action("test.sh");
        arguments.OutputDirectory.Action("/tmp/output");
        arguments.ApiLevels.Action("28");
        arguments.ApiLevels.Action("29");

        // Act & Assert - should not throw
        arguments.Validate();
    }

    [Fact]
    public void AndroidHeadlessTestCommandArguments_AllowsNeither()
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
    }
}