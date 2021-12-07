﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

#nullable enable
namespace Microsoft.DotNet.XHarness.TestRunners.Common;

public abstract class iOSApplicationEntryPointBase : ApplicationEntryPoint
{
    public override async Task RunAsync()
    {
        var options = ApplicationOptions.Current;
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

        // we generate the logs in two different ways depending if the generate xml flag was
        // provided. If it was, we will write the xml file to the tcp writer if present, else
        // we will write the normal console output using the LogWriter
        using (writer)
        {
            var logger = (writer == null || options.EnableXml) ? new LogWriter(Device) : new LogWriter(Device, writer);
            logger.MinimumLogLevel = MinimumLogLevel.Info;

            // if we have ignore files, ignore those tests
            var runner = await InternalRunAsync(logger);

            WriteResults(runner, options, logger, writer ?? Console.Out);

            logger.Info($"Tests run: {runner.TotalTests} Passed: {runner.PassedTests} Inconclusive: {runner.InconclusiveTests} Failed: {runner.FailedTests} Ignored: {runner.FilteredTests + runner.SkippedTests}");

            if (options.AppEndTag != null)
            {
                logger.Info(options.AppEndTag);
            }

            if (options.TerminateAfterExecution)
            {
                TerminateWithSuccess();
            }
        }
    }
}
