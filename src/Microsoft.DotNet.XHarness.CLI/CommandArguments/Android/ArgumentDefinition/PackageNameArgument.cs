using System;
using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Android
{
    internal class PackageNameArgument : StringArgument
    {
        public PackageNameArgument()
            : base("package-name=|p=", "Package name contained within the supplied APK")
        {
        }

        public override void Validate()
        {
            if (string.IsNullOrEmpty(Value))
            {
                throw new ArgumentNullException("Package name not specified");
            }
        }
    }
}
