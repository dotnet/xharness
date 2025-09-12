// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;
using Xunit;

namespace Microsoft.DotNet.XHarness.CLI.Tests.CommandArguments.Android;

public class ApiLevelsArgumentTests
{
    [Fact]
    public void ApiLevelsArgument_CanParseMultipleValues()
    {
        // Arrange
        var argument = new ApiLevelsArgument();

        // Act
        argument.Action("28");
        argument.Action("29");
        argument.Action("30");

        // Assert
        Assert.Equal(3, argument.Value.Count());
        Assert.Contains("28", argument.Value);
        Assert.Contains("29", argument.Value);
        Assert.Contains("30", argument.Value);
        
        Assert.Equal(new[] { 28, 29, 30 }, argument.ApiLevels);
    }

    [Fact]
    public void ApiLevelsArgument_ValidatesApiLevelRange()
    {
        // Arrange
        var argument = new ApiLevelsArgument();
        argument.Action("15"); // Below minimum
        argument.Action("36"); // Above maximum

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => argument.Validate());
        Assert.Contains("API level 15 is not supported", exception.Message);
    }

    [Fact]
    public void ApiLevelsArgument_ValidatesNumericValues()
    {
        // Arrange
        var argument = new ApiLevelsArgument();
        argument.Action("not-a-number");

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => argument.Validate());
        Assert.Contains("API level 'not-a-number' must be an integer", exception.Message);
    }

    [Fact]
    public void ApiLevelsArgument_AcceptsValidRange()
    {
        // Arrange
        var argument = new ApiLevelsArgument();
        argument.Action("16"); // Minimum
        argument.Action("28"); // Common level
        argument.Action("35"); // Maximum

        // Act & Assert - should not throw
        argument.Validate();
        
        Assert.Equal(new[] { 16, 28, 35 }, argument.ApiLevels);
    }
}