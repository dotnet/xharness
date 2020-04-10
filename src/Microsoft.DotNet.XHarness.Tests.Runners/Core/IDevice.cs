namespace Microsoft.DotNet.XHarness.Tests.Runners.Core
{
    /// <summary>
    /// Interface to be implemented by those classes that provide the required
    /// information of the device that is being used so that we can add the
    /// device information in the test logs.
    /// </summary>
    public interface IDevice
    {
        string BundleIdentifier { get; }
        string UniqueIdentifier { get; }
        string Name { get; }
        string Model { get; }
        string SystemName { get; }
        string SystemVersion { get; }
        string Locale { get; }
    }
}
