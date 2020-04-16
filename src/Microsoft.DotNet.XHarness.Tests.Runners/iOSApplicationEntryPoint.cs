// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Tests.Runners.Xunit;

namespace Microsoft.DotNet.XHarness.Tests.Runners.Core
{
    public abstract class iOSApplicatonEntryPoint : ApplicationEntryPoint
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
            var testAssemblies = GetTestAssemblies();
            TestRunner runner;
            switch (TestRunner)
            {
                case TestRunnerType.NUnit:
                    throw new NotImplementedException();
                default:
                    runner = new XUnitTestRunner(logger)
                    {
                        MaxParallelThreads = MaxParallelThreads
                    };
                    break;
            }

            if (!string.IsNullOrEmpty(IgnoreFilesDirectory))
            {
                var categories = await IgnoreFileParser.ParseTraitsContentFileAsync(IgnoreFilesDirectory, TestRunner == TestRunnerType.Xunit);
                // add category filters if they have been added
                runner.SkipCategories(categories);

                var skippedTests = await IgnoreFileParser.ParseContentFilesAsync(IgnoreFilesDirectory);
                if (skippedTests.Any())
                {
                    // ensure that we skip those tests that have been passed via the ignore files
                    runner.SkipTests(skippedTests);
                }
            }

            // if we have ignore files, ignore those tests
            await runner.Run(testAssemblies).ConfigureAwait(false);

            Core.TestRunner.Jargon jargon = Core.TestRunner.Jargon.NUnitV3;
            switch (options.XmlVersion)
            {
                case XmlVersion.NUnitV2:
                    jargon = Core.TestRunner.Jargon.NUnitV2;
                    break;
                case XmlVersion.NUnitV3:
                default: // nunitv3 gives os the most amount of possible details
                    jargon = Core.TestRunner.Jargon.NUnitV3;
                    break;
            }
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

            logger.Info($"Tests run: {runner.TotalTests} Passed: {runner.PassedTests} Inconclusive: {runner.InconclusiveTests} Failed: {runner.FailedTests} Ignored: {runner.FilteredTests}");
            if (options.TerminateAfterExecution)
                TerminateWithSuccess();
        }

    }
}
