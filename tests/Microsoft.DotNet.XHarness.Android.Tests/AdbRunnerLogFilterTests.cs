// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace Microsoft.DotNet.XHarness.Android.Tests;

public class AdbRunnerLogFilterTests
{
    [Fact]
    public void FilterToDotnetLines_FiltersCorrectly()
    {
        var logcat = string.Join('\n', new[]
        {
            "03-24 21:14:29.731 15103 17813 I EuiccGoogle: some noise",
            "03-24 21:14:30.240 17856 17873 I DOTNET  : Extracting asset to /data/user/0/net.dot.Tests/files/System.Linq.dll",
            "03-24 21:14:30.247 16176 17499 E SpeechMicro: Hotword model is single-channel neuralnet.",
            "03-24 21:15:34.276 17856 25041 I DOTNET  : === TEST EXECUTION SUMMARY ===",
            "03-24 21:15:34.276 17856 25041 I DOTNET  : Tests run: 2686 Passed: 2247 Failed: 26",
            "03-24 21:15:34.281 17856 17873 D DOTNET  : Exit code: 1.",
        });

        var result = AdbRunner.FilterToDotnetLines(logcat);

        Assert.Contains("Extracting asset", result);
        Assert.Contains("TEST EXECUTION SUMMARY", result);
        Assert.Contains("Tests run: 2686", result);
        Assert.Contains("Exit code: 1", result);
        Assert.DoesNotContain("EuiccGoogle", result);
        Assert.DoesNotContain("SpeechMicro", result);
    }

    [Fact]
    public void FilterToDotnetLines_EmptyInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, AdbRunner.FilterToDotnetLines(""));
        Assert.Equal(string.Empty, AdbRunner.FilterToDotnetLines(null!));
    }

    [Fact]
    public void FilterToDotnetLines_NoDotnetLines_ReturnsEmpty()
    {
        var logcat = "03-24 21:14:29.731 15103 17813 I EuiccGoogle: noise\n03-24 21:14:29.731 15103 17813 I Finsky: more noise";

        var result = AdbRunner.FilterToDotnetLines(logcat);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void FilterToDotnetLines_HandlesWindowsLineEndings()
    {
        var logcat = "noise line\r\n03-24 21:15:34.276 17856 25041 I DOTNET  : test output\r\nmore noise\r\n";

        var result = AdbRunner.FilterToDotnetLines(logcat);

        Assert.Contains("DOTNET  : test output", result);
        Assert.DoesNotContain("noise line", result);
    }
}
