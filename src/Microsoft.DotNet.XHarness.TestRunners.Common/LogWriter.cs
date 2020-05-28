// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Microsoft.DotNet.XHarness.TestRunners.Common
{
    public class LogWriter
    {
        TextWriter writer;
        IDevice device;

        public MinimumLogLevel MinimumLogLevel { get; set; } = MinimumLogLevel.Info;

        public LogWriter() : this(null, Console.Out) { }

        public LogWriter(IDevice device) : this(device, Console.Out) { }

        public LogWriter(TextWriter w) : this(null, w) { }

        public LogWriter(IDevice device, TextWriter writer)
        {
            this.writer = writer ?? Console.Out;
            this.device = device;
            if (this.device != null) // we just write the header if we do have the device info
                InitLogging();
        }

        [System.Runtime.InteropServices.DllImport("/usr/lib/libobjc.dylib")]
        static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

        public void InitLogging()
        {
            // print some useful info
            writer.WriteLine("[Runner executing:\t{0}]", "Run everything");
            writer.WriteLine("[{0}:\t{1} v{2}]", device.Model, device.SystemName, device.SystemVersion);
            writer.WriteLine("[Device Name:\t{0}]", device.Name);
            writer.WriteLine("[Device UDID:\t{0}]", device.UniqueIdentifier);
            writer.WriteLine("[Device Locale:\t{0}]", device.Locale);
            writer.WriteLine("[Device Date/Time:\t{0}]", DateTime.Now); // to match earlier C.WL output
            writer.WriteLine("[Bundle:\t{0}]", device.BundleIdentifier);
        }
        public void OnError(string message)
        {
            if (MinimumLogLevel < MinimumLogLevel.Error)
                return;
            writer.WriteLine(message);
            writer.Flush();
        }

        public void OnWarning(string message)
        {
            if (MinimumLogLevel < MinimumLogLevel.Warning)
                return;
            writer.WriteLine(message);
            writer.Flush();
        }

        public void OnDebug(string message)
        {
            if (MinimumLogLevel < MinimumLogLevel.Debug)
                return;
            writer.WriteLine(message);
            writer.Flush();
        }

        public void OnDiagnostic(string message)
        {
            if (MinimumLogLevel < MinimumLogLevel.Verbose)
                return;
            writer.WriteLine(message);
            writer.Flush();
        }

        public void OnInfo(string message)
        {
            if (MinimumLogLevel < MinimumLogLevel.Info)
                return;
            writer.WriteLine(message);
            writer.Flush();
        }

        public void Info(string message)
        {
            writer.WriteLine(message);
            writer.Flush();
        }

    }
}
