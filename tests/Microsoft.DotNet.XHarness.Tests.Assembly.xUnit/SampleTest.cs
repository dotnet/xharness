using System;
using Xunit;

namespace Microsoft.DotNet.XHarness.Tests.Assembly.xUnit
{
    public class SampleTest
    {
        [Fact]
        public void PassingTest()
        {
            Assert.False(false, "Expected to pass.");
        }

        [Fact]
        public void FailingTest()
        {
            Assert.False(true, "Expected to fail.");
        }

        [Fact]
        [ActiveIssue("https://github.com/dotnet/xharness/issues/53")]
        public void ActiveIssueTest()
        {
            Assert.False(true, "Should have been skipped.");
        }
    }
}
