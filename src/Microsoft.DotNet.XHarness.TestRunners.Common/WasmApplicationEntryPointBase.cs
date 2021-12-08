using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.XHarness.TestRunners.Common;

public abstract class WasmApplicationEntryPointBase : ApplicationEntryPoint, IDevice
{

    public string BundleIdentifier => string.Empty;

    public string UniqueIdentifier => string.Empty;

    public string Name => string.Empty;

    public string Model => string.Empty;

    public string SystemName => string.Empty;

    public string SystemVersion => string.Empty;

    public string Locale => string.Empty;

    protected override int? MaxParallelThreads => 1;

    protected override IDevice Device => this;

    public override async Task RunAsync()
    {
        var options = ApplicationOptions.Current;
        // we generate the logs in two different ways depending if the generate xml flag was
        // provided. If it was, we will write the xml file to the tcp writer if present, else
        // we will write the normal console output using the LogWriter
        var logger = new LogWriter(Device);
        logger.MinimumLogLevel = MinimumLogLevel.Info;

        var runner = await InternalRunAsync(logger);
        WriteResults(runner, options, logger, Console.Out);

        logger.Info($"Tests run: {runner.TotalTests} Passed: {runner.PassedTests} Inconclusive: {runner.InconclusiveTests} Failed: {runner.FailedTests} Ignored: {runner.FilteredTests}");

        if (options.AppEndTag != null)
        {
            logger.Info(options.AppEndTag);
        }

        LastRunHadFailedTests = runner.FailedTests != 0;
    }

    public bool LastRunHadFailedTests { get; set; }

    protected override void TerminateWithSuccess() => Environment.Exit(0);
}
