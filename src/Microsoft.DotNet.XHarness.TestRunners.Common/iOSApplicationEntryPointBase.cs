// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Threading.Tasks;
using System.IO;

#nullable enable
namespace Microsoft.DotNet.XHarness.TestRunners.Common;

public abstract class iOSApplicationEntryPointBase : ApplicationEntryPoint
{
    /// <summary>
    /// Logger used for outputting logs. Defaults to Console.Out.
    /// </summary>
    public TextWriter? Logger = Console.Out;

    /// <summary>
    /// The final path where test results in XML format will be saved.
    /// </summary>
    public string TestsResultsFinalPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "test-results.xml");

    public override async Task RunAsync()
    {
        var options = ApplicationOptions.Current;

        // On iOS 18 and later, transferring results over a TCP tunnel isn’t supported.
        // Instead, copy the results file from the device to the host machine.
        if (!OperatingSystem.IsMacCatalyst() && Environment.OSVersion.Version.Major >= 18)
        {
            using TextWriter? resultsFileMaybe = options.EnableXml ? System.IO.File.CreateText(TestsResultsFinalPath) : null;
            await InternalRunAsync(options, Logger, resultsFileMaybe);
            Console.WriteLine($"Test results saved to: {TestsResultsFinalPath}");

            if (CoverageResultPath != null)
            {
                Console.WriteLine($"Coverage results saved to: {CoverageResultPath}");
            }
        }
        else
        {
            TcpTextWriter? writer;

            try
            {
                writer = options.UseTunnel
                    ? TcpTextWriter.InitializeWithTunnelConnection(options.HostPort)
                    : TcpTextWriter.InitializeWithDirectConnection(options.HostName, options.HostPort);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to initialize TCP writer. Continuing on console." + Environment.NewLine + ex);
                writer = null; // null means we will fall back to console output
            }

            using (writer)
            {
                var logger = (writer == null || options.EnableXml) ? new LogWriter(Device) : new LogWriter(Device, writer);
                logger.MinimumLogLevel = MinimumLogLevel.Info;

                await InternalRunAsync(options, writer, writer);

                // Coverage file is written to disk by CoverageManager (same as iOS 18+ path).
                // Do NOT send coverage data over TCP — it would corrupt the test results XML stream.
                // The host will pull the coverage file from the app container.
                if (CoverageResultPath != null)
                {
                    Console.WriteLine($"Coverage results saved to: {CoverageResultPath}");
                }
            }
        }
    }
}
