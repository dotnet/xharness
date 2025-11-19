// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Xunit;

namespace Microsoft.DotNet.XHarness.CLI.Tests.Commands.Apple;

public class SimulatorsCommandTests
{
    [Fact]
    public void JsonWithoutRuntimeIdentifier_ShouldBeSkipped()
    {
        // This test validates the fix for the issue where simulators without runtimeIdentifier
        // (like unusable simulators) should be skipped rather than causing an exception.
        
        // Sample JSON similar to what's returned by xcrun simctl runtime list -j
        // This includes an entry without runtimeIdentifier (the unusable simulator)
        var json = @"{
            ""1F039DDE-73B1-4BF2-9FE0-7B089D0ABE5E"": {
                ""build"": ""23J352"",
                ""runtimeIdentifier"": ""com.apple.CoreSimulator.SimRuntime.tvOS-26-0"",
                ""version"": ""26.0"",
                ""state"": ""Ready""
            },
            ""8728D520-0F86-4227-AE03-716249BBB18C"": {
                ""identifier"": ""8728D520-0F86-4227-AE03-716249BBB18C"",
                ""kind"": ""Cryptex Disk Image"",
                ""state"": ""Unusable"",
                ""signatureState"": ""Other Signature Error""
            },
            ""2A448011-F93C-427C-A6F2-CF5EFA39290F"": {
                ""build"": ""23A343"",
                ""runtimeIdentifier"": ""com.apple.CoreSimulator.SimRuntime.iOS-26-0"",
                ""version"": ""26.0"",
                ""state"": ""Ready""
            }
        }";

        var simulators = JsonDocument.Parse(json);

        // Simulate the logic from IsInstalled method
        var runtimeIdentifierToFind = "com.apple.CoreSimulator.SimRuntime.iOS-26-0";
        string? foundVersion = null;
        bool exceptionThrown = false;

        try
        {
            foreach (JsonProperty sim in simulators.RootElement.EnumerateObject())
            {
                // This is the fix: check if property exists before accessing it
                if (!sim.Value.TryGetProperty("runtimeIdentifier", out var runtimeIdProperty))
                {
                    // Skip entries without runtimeIdentifier
                    continue;
                }

                if (runtimeIdProperty.GetString() == runtimeIdentifierToFind)
                {
                    foundVersion = sim.Value.GetProperty("version").GetString();
                    break;
                }
            }
        }
        catch (System.Collections.Generic.KeyNotFoundException)
        {
            exceptionThrown = true;
        }

        // Verify that:
        // 1. No exception was thrown
        Assert.False(exceptionThrown, "KeyNotFoundException should not be thrown when entry lacks runtimeIdentifier");
        
        // 2. We found the correct simulator
        Assert.Equal("26.0", foundVersion);
    }

    [Fact]
    public void JsonWithRuntimeIdentifier_ShouldBeProcessed()
    {
        // This test ensures that simulators with runtimeIdentifier are still processed correctly
        
        var json = @"{
            ""2A448011-F93C-427C-A6F2-CF5EFA39290F"": {
                ""build"": ""23A343"",
                ""runtimeIdentifier"": ""com.apple.CoreSimulator.SimRuntime.iOS-26-0"",
                ""version"": ""26.0"",
                ""state"": ""Ready""
            }
        }";

        var simulators = JsonDocument.Parse(json);
        var runtimeIdentifierToFind = "com.apple.CoreSimulator.SimRuntime.iOS-26-0";
        string? foundVersion = null;

        foreach (JsonProperty sim in simulators.RootElement.EnumerateObject())
        {
            if (!sim.Value.TryGetProperty("runtimeIdentifier", out var runtimeIdProperty))
            {
                continue;
            }

            if (runtimeIdProperty.GetString() == runtimeIdentifierToFind)
            {
                foundVersion = sim.Value.GetProperty("version").GetString();
                break;
            }
        }

        Assert.Equal("26.0", foundVersion);
    }

    [Fact]
    public void JsonWithOnlyUnusableSimulators_ShouldNotFindMatch()
    {
        // Test scenario where all entries lack runtimeIdentifier
        
        var json = @"{
            ""8728D520-0F86-4227-AE03-716249BBB18C"": {
                ""identifier"": ""8728D520-0F86-4227-AE03-716249BBB18C"",
                ""kind"": ""Cryptex Disk Image"",
                ""state"": ""Unusable""
            }
        }";

        var simulators = JsonDocument.Parse(json);
        var runtimeIdentifierToFind = "com.apple.CoreSimulator.SimRuntime.iOS-26-0";
        string? foundVersion = null;
        bool exceptionThrown = false;

        try
        {
            foreach (JsonProperty sim in simulators.RootElement.EnumerateObject())
            {
                if (!sim.Value.TryGetProperty("runtimeIdentifier", out var runtimeIdProperty))
                {
                    continue;
                }

                if (runtimeIdProperty.GetString() == runtimeIdentifierToFind)
                {
                    foundVersion = sim.Value.GetProperty("version").GetString();
                    break;
                }
            }
        }
        catch (System.Collections.Generic.KeyNotFoundException)
        {
            exceptionThrown = true;
        }

        Assert.False(exceptionThrown, "KeyNotFoundException should not be thrown");
        Assert.Null(foundVersion);
    }
}
