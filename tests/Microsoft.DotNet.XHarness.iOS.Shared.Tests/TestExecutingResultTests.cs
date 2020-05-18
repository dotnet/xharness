// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests
{
    public class TestExecutingResultTests
    {
        [Theory]
        [InlineData(TestExecutingResult.Building)]
        [InlineData(TestExecutingResult.BuildQueued)]
        [InlineData(TestExecutingResult.Built)]
        [InlineData(TestExecutingResult.Running)]
        [InlineData(TestExecutingResult.RunQueued)]
        public void InProgressFlagIsPresent(TestExecutingResult result)
        {
            Assert.True(result.HasFlag(TestExecutingResult.InProgress));
        }

        [Theory]
        [InlineData(TestExecutingResult.Crashed)]
        [InlineData(TestExecutingResult.TimedOut)]
        [InlineData(TestExecutingResult.HarnessException)]
        [InlineData(TestExecutingResult.LaunchFailure)]
        [InlineData(TestExecutingResult.BuildFailure)]
        [InlineData(TestExecutingResult.Failed)]
        public void FailedFlaggedIsPresent(TestExecutingResult result)
        {
            Assert.True(result.HasFlag(TestExecutingResult.Failed));
        }

        [Theory]
        [InlineData(TestExecutingResult.Succeeded)]
        [InlineData(TestExecutingResult.Failed)]
        [InlineData(TestExecutingResult.Ignored)]
        [InlineData(TestExecutingResult.DeviceNotFound)]
        [InlineData(TestExecutingResult.Finished)]
        public void FinishedFlagIsPresent(TestExecutingResult result)
        {
            Assert.True(result.HasFlag(TestExecutingResult.Finished));
        }

        [Theory]
        [InlineData(TestExecutingResult.Succeeded)]
        public void FailedFlaggedIsNotPresent(TestExecutingResult result)
        {
            Assert.False(result.HasFlag(TestExecutingResult.Failed));
        }

        [Theory]
        [InlineData(TestExecutingResult.Crashed)]
        [InlineData(TestExecutingResult.TimedOut)]
        [InlineData(TestExecutingResult.HarnessException)]
        [InlineData(TestExecutingResult.LaunchFailure)]
        [InlineData(TestExecutingResult.BuildFailure)]
        [InlineData(TestExecutingResult.Failed)]
        public void SucceededFlaggedIsNotPresent(TestExecutingResult result)
        {
            Assert.False(result.HasFlag(TestExecutingResult.Succeeded));
        }
    }
}
