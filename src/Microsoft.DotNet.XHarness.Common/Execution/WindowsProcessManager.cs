﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.DotNet.XHarness.Common.Logging;

namespace Microsoft.DotNet.XHarness.Common.Execution
{
    public class WindowsProcessManager : ProcessManager
    {
        // We cannot enumerate processes well on Windows but we will use the CLI as .NET Core 3.1 and will kill the whole process tree
        // (this library is only used under netstandard 2.1 in Xamarin where it runs on OSX only)
        protected override List<int> GetChildProcessIds(ILog log, int pid) => new List<int> { pid };

        protected override int Kill(int pid, int sig)
        {
#if NETCOREAPP3_1
            Process.GetProcessById(pid).Kill(entireProcessTree: true);
#else
            Process.GetProcessById(pid).Kill();
#endif
            return 0;
        }
    }
}
