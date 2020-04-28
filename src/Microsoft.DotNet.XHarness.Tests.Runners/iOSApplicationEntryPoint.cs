// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

namespace Microsoft.DotNet.XHarness.Tests.Runners.Core
{
    public abstract class iOSApplicationEntryPoint : ApplicationEntryPoint
    {
        public override async Task RunAsync()
        {
            var options = ApplicationOptions.Current;
            TcpTextWriter writer = null;
            if (!string.IsNullOrEmpty(options.HostName))
            {
                try
                {
                    writer = new TcpTextWriter(options.HostName, options.HostPort);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Network error: Cannot connect to {0}:{1}: {2}. Continuing on console.", options.HostName, options.HostPort, ex);
                    writer = null; // will default to the console
                }
            }

            // we generate the logs in two different ways depending if the generate xml flag was
            // provided. If it was, we will write the xml file to the tcp writer if present, else
            // we will write the normal console output using the LogWriter
            var logger = (writer == null || options.EnableXml) ? new LogWriter(Device) : new LogWriter(Device, writer);
            logger.MinimumLogLevel = MinimumLogLevel.Info;

            // if we have ignore files, ignore those tests
            var runner = await InternalRunAsync(logger);

            TestRunner.Jargon jargon = options.XmlVersion.ToTestRunnerJargon();

            if (options.EnableXml)
            {
                runner.WriteResultsToFile(writer ?? Console.Out, jargon);
                logger.Info("Xml file was written to the tcp listener.");
            }
            else
            {
                string resultsFilePath = runner.WriteResultsToFile(jargon);
                logger.Info($"Xml result can be found {resultsFilePath}");
            }

            logger.Info($"Tests run: {runner.TotalTests} Passed: {runner.PassedTests} Inconclusive: {runner.InconclusiveTests} Failed: {runner.FailedTests} Ignored: {runner.FilteredTests + runner.SkippedTests}");

            if (options.TerminateAfterExecution)
            {
                TerminateWithSuccess();
            }
        }

    }
}
