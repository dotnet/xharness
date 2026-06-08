// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.DotNet.XHarness.Common.Tests;

public class CommandDiagnosticsTests
{
    [Fact]
    public void SaveToJsonFile_IncludesEnvironment()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            using var loggerFactory = LoggerFactory.Create(_ => { });
            var diagnostics = new CommandDiagnostics(loggerFactory.CreateLogger("test"), TargetPlatform.Android, "test")
            {
                ExitCode = ExitCode.SUCCESS,
                Environment = new ExecutionEnvironmentInfo
                {
                    Host = new HostEnvironmentInfo
                    {
                        MachineName = "host1",
                        OperatingSystem = "TestOS",
                    },
                    Target = new TargetEnvironmentInfo
                    {
                        Kind = "emulator",
                        Identifier = "emulator-5554",
                    },
                },
            };

            diagnostics.SaveToJsonFile(tempPath);

            using var document = JsonDocument.Parse(File.ReadAllText(tempPath));
            var root = document.RootElement[0];
            var environment = root.GetProperty("environment");
            Assert.Equal("host1", environment.GetProperty("host").GetProperty("machineName").GetString());
            Assert.Equal("emulator-5554", environment.GetProperty("target").GetProperty("identifier").GetString());
        }
        finally
        {
            File.Delete(tempPath);
        }
    }
}
