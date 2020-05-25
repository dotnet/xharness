using Xunit;

namespace DummyTestProject
{
    public class DummyTests
    {
        [Fact]
        public void PassingTest()
        {
            Assert.Equal(1, 1);
        }

        [Fact]
        public void FailingTest()
        {
            Assert.Equal(1, 2);
        }

        [Fact(Skip = "Skipped reason")]
        public void SkippedTest()
        {
            Assert.Equal(1, 1);
        }
    }
}
