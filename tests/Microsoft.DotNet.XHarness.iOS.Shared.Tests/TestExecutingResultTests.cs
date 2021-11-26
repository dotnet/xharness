// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests;

public class TestExecutingResultTests
{
    [Theory]
    [InlineData(
        new[]
        {
                TestExecutingResult.Crashed,
                TestExecutingResult.TimedOut,
                TestExecutingResult.HarnessException,
                TestExecutingResult.LaunchFailure,
                TestExecutingResult.BuildFailure,
                TestExecutingResult.Failed,
        },
        TestExecutingResult.Failed
    )]
    [InlineData(
        new[]
        {
                TestExecutingResult.Building,
                TestExecutingResult.BuildQueued,
                TestExecutingResult.Built,
                TestExecutingResult.Running,
                TestExecutingResult.RunQueued,
                TestExecutingResult.InProgress,
                TestExecutingResult.StateMask,
        },
        TestExecutingResult.InProgress
    )]
    [InlineData(
        new[]
        {
                TestExecutingResult.Succeeded,
                TestExecutingResult.BuildSucceeded,
        },
        TestExecutingResult.Succeeded
    )]
    public void FlaggedIsPresentWhereItShouldBe(TestExecutingResult[] withFlag, TestExecutingResult flag)
    {
        var withoutFlag = Enum.GetValues(typeof(TestExecutingResult))
            .Cast<TestExecutingResult>()
            .Except(withFlag);

        foreach (var result in withoutFlag)
        {
            Assert.False(result.HasFlag(flag), $"{result} should not have {flag}");
        }

        foreach (var result in withFlag)
        {
            Assert.True(result.HasFlag(flag), $"{result} should have {flag}");
        }
    }
}
