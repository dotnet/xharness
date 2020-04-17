// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Tests.Runners.Core;

namespace Microsoft.DotNet.XHarness.Tests.Runners
{
    public abstract class AndroidApplicationEntryPoint : ApplicationEntryPoint
    {
        /// <summary>
        /// Implementors should provide a text writter than will be used to
        /// write the logging of the tests that are executed.
        /// </summary>
        public abstract TextWriter Logger { get; }

        /// <summary>
        /// Implementors should provide a full path in which the final 
        /// results of the test run will be written. This property must not
        /// return null.
        /// </summary>
        public abstract string TestsResultsFinalPath { get; }


        public override async Task RunAsync()
        {
            var options = ApplicationOptions.Current;
            // we generate the logs in two different ways depending if the generate xml flag was
            // provided. If it was, we will write the xml file to the tcp writer if present, else
            // we will write the normal console output using the LogWriter
            var logger = (Logger == null || options.EnableXml) ? new LogWriter(Device) : new LogWriter(Device, Logger);
            logger.MinimumLogLevel = MinimumLogLevel.Info;
            var testAssemblies = GetTestAssemblies();

            var runner = await InternalRunAsync(logger);

            TestRunner.Jargon jargon = Core.TestRunner.Jargon.NUnitV3;
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
                if (TestsResultsFinalPath == null)
                    throw new InvalidOperationException("Tests results final path cannot be null.");
                using (var stream = File.Create(TestsResultsFinalPath))
                using (var writer = new StreamWriter(stream))
                {
                    runner.WriteResultsToFile(writer, jargon);
                    logger.Info($"Xml file was written to {TestsResultsFinalPath}.");
                }
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
