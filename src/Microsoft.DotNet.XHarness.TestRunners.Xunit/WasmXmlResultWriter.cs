using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Microsoft.DotNet.XHarness.TestRunners.Xunit
{
    public class WasmXmlResultWriter
    {
        public static void WriteOnSingleLine(XElement assembliesElement)
        {
            using var ms = new MemoryStream();
            assembliesElement.Save(ms);
            ms.TryGetBuffer(out var bytes);
            var base64 = Convert.ToBase64String(bytes, Base64FormattingOptions.None);
            Console.WriteLine($"STARTRESULTXML {bytes.Count} {base64} ENDRESULTXML");
            Console.WriteLine($"Finished writing {bytes.Count} bytes of RESULTXML");
        }
    }
}
