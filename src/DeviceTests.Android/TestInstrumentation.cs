using Android.App;
using Android.OS;
using Microsoft.DotNet.XHarness.InstrumentationBase.Xunit;
using System.Collections.Generic;

namespace DeviceTests.Droid
{
    [Instrumentation(Name = "net.dot.xharness.devicetests.TestInstrumentation")]
    public class TestInstrumentation : AndroidInstrumentationBase
    {
        protected override IEnumerable<string> Tests
        {
            get
            {
                yield return typeof(TestingTests).Assembly.Location;
            }
        }
    }
}
