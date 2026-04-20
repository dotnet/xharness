// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

#nullable enable
namespace Microsoft.DotNet.XHarness.TestRunners.Common;

public abstract class WasmApplicationEntryPointBase : ApplicationEntryPoint
{
    protected override int? MaxParallelThreads => 1;

    protected override IDevice? Device => null;

    public override async Task RunAsync()
    {
        var options = ApplicationOptions.Current;
        var runner = await InternalRunAsync(options, null, Console.Out);

        LastRunHadFailedTests = runner.FailedTests != 0;

        // Emit coverage data on stdout for the host to capture
        if (CoverageResultPath != null)
        {
            try
            {
                var coverageBytes = System.IO.File.ReadAllBytes(CoverageResultPath);
                var base64 = Convert.ToBase64String(coverageBytes);
                Console.WriteLine($"STARTCOVERAGEXML {coverageBytes.Length} {base64} ENDCOVERAGEXML");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Coverage] Warning: Failed to emit coverage data: {ex.Message}");
            }
        }
    }

    public bool LastRunHadFailedTests { get; set; }

    protected override void TerminateWithSuccess() => Environment.Exit(0);
}
