using System;
using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Android
{
    /// <summary>
    /// Tests classes to be included in the run while all others are ignored.
    /// </summary>
    internal class DeviceArchitectureArgument : RepetableArgument
    {
        public DeviceArchitectureArgument()
            : base("device-arch=",
                "If specified, forces running on a device with given architecture (x86, x86_64, arm64-v8a or armeabi-v7a). Otherwise inferred from supplied APK. " +
                "Can be used more than once.")
        {
        }

        public override void Validate()
        {
            foreach (var archName in Value)
            {
                try
                {
                    AndroidArchitectureHelper.ParseAsAndroidArchitecture(archName);
                }
                catch (ArgumentOutOfRangeException)
                {
                    throw new ArgumentException(
                        $"Failed to parse architecture '{archName}'. Available architectures are:" +
                        GetAllowedValues<AndroidArchitecture>(t => t.AsString()));
                }
            }
        }
    }
}
