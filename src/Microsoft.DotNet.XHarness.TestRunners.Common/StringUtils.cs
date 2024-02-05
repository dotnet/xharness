using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.XHarness.TestRunners.Common;

public class StringUtils
{
    public static string EscapeNewLines(string message)
        => message.Replace("\r", "\\r").Replace("\n", "\\n");
}
