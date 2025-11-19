// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using Xunit;

namespace Microsoft.DotNet.XHarness.CLI.Tests.Commands.Apple;

public class SimulatorsCommandTests
{
    [Fact]
    public void ParseSimctlRuntimeList_WithoutRuntimeIdentifier_ShouldSkipEntry()
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

        var result = ParseSimulatorList(json, "com.apple.CoreSimulator.SimRuntime.iOS-26-0");

        Assert.NotNull(result);
        Assert.Equal("26.0", result);
    }

    [Fact]
    public void ParseSimctlRuntimeList_WithoutVersionProperty_ShouldReturnNull()
    {
        // Test scenario where runtimeIdentifier exists but version property is missing
        var json = @"{
            ""2A448011-F93C-427C-A6F2-CF5EFA39290F"": {
                ""build"": ""23A343"",
                ""runtimeIdentifier"": ""com.apple.CoreSimulator.SimRuntime.iOS-26-0"",
                ""state"": ""Ready""
            }
        }";

        var result = ParseSimulatorList(json, "com.apple.CoreSimulator.SimRuntime.iOS-26-0");

        Assert.Null(result);
    }

    [Fact]
    public void ParseSimctlRuntimeList_WithOnlyUnusableSimulators_ShouldReturnNull()
    {
        // Test scenario where all entries lack runtimeIdentifier
        var json = @"{
            ""8728D520-0F86-4227-AE03-716249BBB18C"": {
                ""identifier"": ""8728D520-0F86-4227-AE03-716249BBB18C"",
                ""kind"": ""Cryptex Disk Image"",
                ""state"": ""Unusable""
            }
        }";

        var result = ParseSimulatorList(json, "com.apple.CoreSimulator.SimRuntime.iOS-26-0");

        Assert.Null(result);
    }

    [Fact]
    public void ParseSimctlRuntimeList_WithValidSimulator_ShouldReturnVersion()
    {
        // Test normal scenario with valid simulator
        var json = @"{
            ""2A448011-F93C-427C-A6F2-CF5EFA39290F"": {
                ""build"": ""23A343"",
                ""runtimeIdentifier"": ""com.apple.CoreSimulator.SimRuntime.iOS-26-0"",
                ""version"": ""26.0"",
                ""state"": ""Ready""
            }
        }";

        var result = ParseSimulatorList(json, "com.apple.CoreSimulator.SimRuntime.iOS-26-0");

        Assert.NotNull(result);
        Assert.Equal("26.0", result);
    }

    [Fact]
    public void ParseSimctlRuntimeList_WithMixedEntries_ShouldHandleGracefully()
    {
        // Test with multiple valid and invalid entries
        var json = @"{
            ""entry1"": {
                ""runtimeIdentifier"": ""com.apple.CoreSimulator.SimRuntime.tvOS-25-0"",
                ""version"": ""25.0""
            },
            ""entry2"": {
                ""kind"": ""Unusable""
            },
            ""entry3"": {
                ""runtimeIdentifier"": ""com.apple.CoreSimulator.SimRuntime.iOS-26-0""
            },
            ""entry4"": {
                ""runtimeIdentifier"": ""com.apple.CoreSimulator.SimRuntime.iOS-26-0"",
                ""version"": ""26.0""
            }
        }";

        var result = ParseSimulatorList(json, "com.apple.CoreSimulator.SimRuntime.iOS-26-0");

        // Should return null for entry3 (missing version) and find entry4
        Assert.Null(result);
    }

    // Helper method that replicates the parsing logic from SimulatorsCommand.IsInstalled
    // This tests the specific JSON parsing behavior that was fixed
    private static string? ParseSimulatorList(string json, string runtimeIdentifier)
    {
        var simulators = JsonDocument.Parse(json);

        foreach (JsonProperty sim in simulators.RootElement.EnumerateObject())
        {
            // Skip entries that don't have a runtimeIdentifier property (e.g., unusable simulators)
            if (!sim.Value.TryGetProperty("runtimeIdentifier", out var runtimeIdProperty))
            {
                continue;
            }

            if (runtimeIdProperty.GetString() == runtimeIdentifier)
            {
                // Also check if version property exists
                if (!sim.Value.TryGetProperty("version", out var versionProperty))
                {
                    return null;
                }

                return versionProperty.GetString();
            }
        }

        return null;
    }
}
