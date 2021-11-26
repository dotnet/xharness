// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

namespace Microsoft.DotNet.XHarness.Android.Execution;

// At some point all the process management APIs should be unified. For now I just added an 's' to ProcessExecutionResult to prevent accidental collision
public class ProcessExecutionResults
{
    public bool TimedOut { get; set; }
    public int ExitCode { get; set; }
    public bool Succeeded => !TimedOut && ExitCode == 0;
    public string StandardOutput { get; set; } = "";
    public string StandardError { get; set; } = "";

    public override string ToString()
    {
        var output = new StringBuilder();
        output.AppendLine($"Exit code: {ExitCode}");
        output.AppendLine($"Standard Output:{Environment.NewLine}{StandardOutput}");
        if (!string.IsNullOrEmpty(StandardError))
        {
            output.AppendLine($"Standard Error:{Environment.NewLine}{StandardError}");
        }
        return output.ToString();
    }

}

// interface that helps to manage the different processes in the app.
public interface IAdbProcessManager
{
    public string DeviceSerial { get; set; }
    public ProcessExecutionResults Run(string filename, string arguments);
    public ProcessExecutionResults Run(string filename, string arguments, TimeSpan timeout);
}
