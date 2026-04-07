// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm;
using Microsoft.DotNet.XHarness.CLI.Commands;
using Microsoft.DotNet.XHarness.CLI.Commands.Wasm;
using Xunit;

namespace Microsoft.DotNet.XHarness.CLI.Tests.Commands;

public class WasmBrowserTestRunnerTests
{
    [Fact]
    public void WasmTestBrowserCommandParsesNoAppVerbosity()
    {
        string outputDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            var arguments = new WasmTestBrowserCommandArguments();
            var command = new UnitTestCommand<WasmTestBrowserCommandArguments>(arguments);

            int exitCode = command.Invoke(new[]
            {
                "--app=app",
                $"--output-directory={outputDirectory}",
                "--no-app-verbosity",
            });

            Assert.Equal(0, exitCode);
            Assert.True(command.CommandRun);
            Assert.True(arguments.NoAppVerbosity);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
                Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void ApplicationArgumentsIncludeVerbosityByDefault()
    {
        var arguments = new WasmTestBrowserCommandArguments();
        arguments.Verbosity.Action("Debug");

        string[] applicationArguments = WasmBrowserTestRunner.GetApplicationArguments(
            arguments,
            new[] { "abc", "foobar" },
            new ServerURLs("http://127.0.0.1:8000", null));

        Assert.Equal(new[] { "abc", "foobar", "-verbosity", "Debug" }, applicationArguments);
    }

    [Fact]
    public void ApplicationArgumentsSkipVerbosityWhenRequested()
    {
        var arguments = new WasmTestBrowserCommandArguments();
        arguments.Verbosity.Action("Debug");
        arguments.NoAppVerbosity.Action(string.Empty);

        string[] applicationArguments = WasmBrowserTestRunner.GetApplicationArguments(
            arguments,
            new[] { "abc", "foobar" },
            new ServerURLs("http://127.0.0.1:8000", null));

        Assert.Equal(new[] { "abc", "foobar" }, applicationArguments);
    }
}
