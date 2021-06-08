using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Android
{
    internal class DeviceIdArgument : StringArgument
    {
        public DeviceIdArgument()
            : base("device-id=", "Device where APK should be installed")
        {
        }
    }
}
