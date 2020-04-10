namespace Microsoft.DotNet.XHarness.Tests.Runners.Core
{
    internal class TestRunSelector
    {
        public string Assembly { get; set; }
        public string Value { get; set; }
        public TestRunSelectorType Type { get; set; }
        public bool Include { get; set; }
    }
}
