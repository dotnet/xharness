// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm
{
    internal class WasmTestCommandArguments : XHarnessCommandArguments, IWebServerArguments
    {
        public JavaScriptEngineArgument Engine { get; } = new();
        public JavaScriptEngineArguments EngineArgs { get; } = new();
        public JavaScriptFileArgument JSFile { get; } = new("runtime.js");
        public ErrorPatternsFileArgument ErrorPatternsFile { get; } = new();
        public ExpectedExitCodeArgument ExpectedExitCode { get; } = new((int)Common.CLI.ExitCode.SUCCESS);
        public OutputDirectoryArgument OutputDirectory { get; } = new();
        public TimeoutArgument Timeout { get; } = new(TimeSpan.FromMinutes(15));

        public WebServerMiddlewareArgument WebServerMiddlewarePathsAndTypes { get; } = new();
        public WebServerHttpEnvironmentVariables WebServerHttpEnvironmentVariables { get; } = new();
        public WebServerHttpsEnvironmentVariables WebServerHttpsEnvironmentVariables { get; } = new();
        public WebServerUseHttpsArguments WebServerUseHttps { get; } = new();
        public WebServerUseCorsArguments WebServerUseCors { get; } = new();
        public WebServerUseCrossOriginPolicyArguments WebServerUseCrossOriginPolicy { get; } = new();

        protected override IEnumerable<Argument> GetArguments() => new Argument[]
        {
            Engine,
            EngineArgs,
            JSFile,
            ErrorPatternsFile,
            OutputDirectory,
            Timeout,
            ExpectedExitCode,
            WebServerMiddlewarePathsAndTypes,
            WebServerHttpEnvironmentVariables,
            WebServerHttpsEnvironmentVariables,
            WebServerUseHttps,
            WebServerUseCors,
            WebServerUseCrossOriginPolicy,
        };
    }
}
