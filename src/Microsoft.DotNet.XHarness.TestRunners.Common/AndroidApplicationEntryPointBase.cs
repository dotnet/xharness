// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

#nullable enable
namespace Microsoft.DotNet.XHarness.TestRunners.Common;

/// <summary>
/// Implementors should provide a text writter than will be used to
/// write the logging of the tests that are executed.
/// </summary>
public abstract class AndroidApplicationEntryPointBase : ApplicationEntryPoint
{
    public abstract TextWriter? Logger { get; }

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

        var runner = await InternalRunAsync(logger);
        if (options.EnableXml)
        {
            if (TestsResultsFinalPath == null)
            {
                throw new InvalidOperationException("Tests results final path cannot be null.");
            }

            using (var stream = File.Create(TestsResultsFinalPath))
            using (var writer = new StreamWriter(stream))
            {
                WriteResults(runner, options, logger, writer);
            }
        }
        else
        {
            WriteResults(runner, options, logger, Console.Out);
        }

        logger.Info($"Tests run: {runner.TotalTests} Passed: {runner.PassedTests} Inconclusive: {runner.InconclusiveTests} Failed: {runner.FailedTests} Ignored: {runner.FilteredTests}");

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
