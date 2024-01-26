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
    {
        using var ms = new MemoryStream();
        AssembliesElement.Save(ms);
        ms.TryGetBuffer(out var bytes);
        var base64 = Convert.ToBase64String(bytes, Base64FormattingOptions.None);
        Console.WriteLine($"STARTRESULTXML {bytes.Count} {base64} ENDRESULTXML");
        Console.WriteLine($"Finished writing {bytes.Count} bytes of RESULTXML");
    }
}
