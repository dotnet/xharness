// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasi;

internal class WasiTestCommandArguments : XHarnessCommandArguments, IWebServerArguments
{
    public WasmEngineArgument Engine { get; } = new();
    public WasmEngineLocationArgument EnginePath { get; } = new();
    public WasmEngineArguments EngineArgs { get; } = new();
    public WasmFileArgument WasmFile { get; } = new("artifacts/bin/native/net7.0-wasi-Debug-wasm/dotnet.wasm");  
    public LibraryArgument LibFile { get; } = new("src/mono/sample/wasi/console/bin/Wasi.Console.Sample.dll");  
    public ErrorPatternsFileArgument ErrorPatternsFile { get; } = new();
    public ExpectedExitCodeArgument ExpectedExitCode { get; } = new((int)Common.CLI.ExitCode.SUCCESS);
    public OutputDirectoryArgument OutputDirectory { get; } = new();
    public TimeoutArgument Timeout { get; } = new(TimeSpan.FromMinutes(15));

    public SymbolMapFileArgument SymbolMapFileArgument { get; } = new();
    public SymbolicatePatternsFileArgument SymbolicatePatternsFileArgument { get; } = new();
    public SymbolicatorArgument SymbolicatorArgument { get; } = new();
    public WebServerMiddlewareArgument WebServerMiddlewarePathsAndTypes { get; } = new();
    public WebServerHttpEnvironmentVariables WebServerHttpEnvironmentVariables { get; } = new();
    public WebServerHttpsEnvironmentVariables WebServerHttpsEnvironmentVariables { get; } = new();
    public WebServerUseHttpsArguments WebServerUseHttps { get; } = new();
    public WebServerUseCorsArguments WebServerUseCors { get; } = new();
    public WebServerUseCrossOriginPolicyArguments WebServerUseCrossOriginPolicy { get; } = new();

    public string SubCommand{ get; } = "run";

    protected override IEnumerable<Argument> GetArguments() => new Argument[]
    {
            Engine,
            EnginePath,
            EngineArgs,
            WasmFile,
            ErrorPatternsFile,
            OutputDirectory,
            Timeout,
            ExpectedExitCode,
            SymbolMapFileArgument,
            SymbolicatePatternsFileArgument,
            SymbolicatorArgument,
            WebServerMiddlewarePathsAndTypes,
            WebServerHttpEnvironmentVariables,
            WebServerHttpsEnvironmentVariables,
            WebServerUseHttps,
            WebServerUseCors,
            WebServerUseCrossOriginPolicy,
    };
}
