using Xunit;

namespace DeviceTests.Droid
{
    public class TestingTests
    {
        [Fact]
        public void Passing()
        {
            Assert.True(true);
        }

        [Fact]
        public void Failing()
        {
            Assert.False(true);
        }

        [Fact(Skip = "SKIPPING")]
        public void Skipped()
        {
            Assert.False(true);
        }
    }
}
