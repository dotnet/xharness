using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.Android.Execution
{
    class NewReportManager : IReportManager
    {
        private readonly ILogger _log;
        public NewReportManager(ILogger log)
        {
            _log = log;
        }

        public void DumpBugReport(AdbRunner runner, string outputFilePath)
        {
            // give some time for bug report to be available
            Thread.Sleep(3000);

            var result = runner.RunAdbCommand($"bugreport {outputFilePath}.zip", TimeSpan.FromMinutes(5));

            if (result.ExitCode != 0)
            {
                // Could throw here, but it would tear down a possibly otherwise acceptable execution.
                _log.LogError($"Error getting ADB bugreport:{Environment.NewLine}{result}");
            }
            else
            {
                _log.LogInformation($"Wrote ADB bugreport to {outputFilePath}.zip");
            }
        }
    }
}
