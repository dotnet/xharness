using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.TestRunners.Common;

namespace Microsoft.DotNet.XHarness.TestRunners.Xunit;

internal class WasmThreadedTestRunner : XUnitTestRunner
{
    public WasmThreadedTestRunner(LogWriter logger) : base(logger)
    {
    }

    public override void WriteResultsToFile(TextWriter writer, XmlResultJargon jargon)
        => WasmXmlResultWriter.WriteOnSingleLine(AssembliesElement);
}
