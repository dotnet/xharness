// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.XHarness.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.CLI.Commands;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.DotNet.XHarness.CLI.Android
{
    internal abstract class AndroidCommand<TArguments> : XHarnessCommand<TArguments> where TArguments : IXHarnessCommandArguments
    {
        protected readonly Lazy<IDiagnosticsData> _diagnosticsData;
        protected IDiagnosticsData DiagnosticsData => _diagnosticsData.Value;

        protected AndroidCommand(string name, bool allowsExtraArgs, string? help = null)
            : base(TargetPlatform.Android, name, allowsExtraArgs, new ServiceCollection(), help)
        {
            _diagnosticsData = new(() => Services.BuildServiceProvider().GetRequiredService<IDiagnosticsData>());
        }

        protected static void FillDiagnosticData(
            IDiagnosticsData data,
            string deviceName,
            int apiVersion,
            IEnumerable<string> apkRequiredArchitecture)
        {
            data.Target = string.Join(",", apkRequiredArchitecture);
            data.TargetOS = "API " + apiVersion;
            data.Device = deviceName;
            data.IsDevice = !deviceName.ToLowerInvariant().StartsWith("emulator");
        }
    }
}
