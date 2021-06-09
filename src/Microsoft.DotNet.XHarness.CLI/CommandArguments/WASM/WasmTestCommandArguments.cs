﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm
{
    internal class WasmTestCommandArguments : XHarnessCommandArguments
    {
        public AppPathArgument AppPackagePath { get; } = new();
        public JavaScriptEngineArgument Engine { get; } = new();
        public JavaScriptEngineArguments EngineArgs { get; } = new();
        public JavaScriptFileArgument JSFile { get; } = new("runtime.js");
        public ExpectedExitCodeArgument ExpectedExitCode { get; } = new((int)Common.CLI.ExitCode.SUCCESS);
        public OutputDirectoryArgument OutputDirectory { get; } = new();
        public TimeoutArgument Timeout { get; } = new(TimeSpan.FromMinutes(15));
        public WebServerMiddlewarePathsAndTypes WebServerMiddlewarePathsAndTypes { get; } = new();
        public HttpWebServerEnvironmentVariables HttpWebServerEnvironmentVariables { get; } = new();
        public HttpsWebServerEnvironmentVariables HttpsWebServerEnvironmentVariables { get; } = new();

        protected override IEnumerable<ArgumentDefinition> GetArguments() => new ArgumentDefinition[]
        {
            AppPackagePath,
            Engine,
            EngineArgs,
            JSFile,
            OutputDirectory,
            Timeout,
            ExpectedExitCode,
            WebServerMiddlewarePathsAndTypes,
            HttpWebServerEnvironmentVariables,
            HttpsWebServerEnvironmentVariables,
        };
    }
}
