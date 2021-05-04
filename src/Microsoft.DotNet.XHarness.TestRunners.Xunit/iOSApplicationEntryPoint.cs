// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.TestRunners.Common;

namespace Microsoft.DotNet.XHarness.TestRunners.Xunit
{
    public abstract class iOSApplicationEntryPoint : ApplicationEntryPoint
    {
        protected override TestRunner GetTestRunner(LogWriter logWriter)
        {
            var runner = new XUnitTestRunner(logWriter) { MaxParallelThreads = MaxParallelThreads };
            ConfigureRunnerFilters(runner, ApplicationOptions.Current);
            return runner;
        }

        protected override bool IsXunit => true;

        public override async Task RunAsync()
        {
            var options = ApplicationOptions.Current;
            var writer = new TcpTextWriter();
            try
            {
                if (options.UseTunnel)
                {
                    writer.InitializeTunnelConnection(options.HostPort);
                }
                else
                {
                    writer.InitializeDirectConnection(options.HostName, options.HostPort);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Cannot connect to {0}:{1}: {2}. Continuing on console.", options.HostName, options.HostPort, ex);
                writer = null; // will default to the console
            }

            // we generate the logs in two different ways depending if the generate xml flag was
            // provided. If it was, we will write the xml file to the tcp writer if present, else
            // we will write the normal console output using the LogWriter
            var logger = (writer == null || options.EnableXml) ? new LogWriter(Device) : new LogWriter(Device, writer);
            logger.MinimumLogLevel = MinimumLogLevel.Info;

            // if we have ignore files, ignore those tests
            var runner = await InternalRunAsync(logger);

            WriteResults(runner, options, logger, writer ?? Console.Out);

            logger.Info($"Tests run: {runner.TotalTests} Passed: {runner.PassedTests} Inconclusive: {runner.InconclusiveTests} Failed: {runner.FailedTests} Ignored: {runner.FilteredTests + runner.SkippedTests}");

            if (options.TerminateAfterExecution)
            {
                TerminateWithSuccess();
            }
        }

    }
}
