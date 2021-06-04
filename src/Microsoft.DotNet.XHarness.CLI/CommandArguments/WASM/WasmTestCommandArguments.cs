// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm
{
    /// <summary>
    /// Specifies a name of a JavaScript engine used to run WASM application.
    /// </summary>
    internal enum JavaScriptEngine
    {
        /// <summary>
        /// V8
        /// </summary>
        V8,
        /// <summary>
        /// JavaScriptCore
        /// </summary>
        JavaScriptCore,
        /// <summary>
        /// SpiderMonkey
        /// </summary>
        SpiderMonkey,
    }

    internal class WasmTestCommandArguments : TestCommandArguments
    {
        private JavaScriptEngine? _engine;

        public JavaScriptEngine Engine
        {
            get => _engine ?? throw new ArgumentException("Engine not specified");
            set => _engine = value;
        }

        public string? ErrorPatternsFile { get; set; }

        public List<string> EngineArgs { get; set; } = new List<string>();

        public string JSFile { get; set; } = "runtime.js";

        public int ExpectedExitCode { get; set; } = (int)Common.CLI.ExitCode.SUCCESS;

        protected override OptionSet GetTestCommandOptions() => new()
        {
            { "engine=|e=", "Specifies the JavaScript engine to be used",
                v => Engine = ParseArgument<JavaScriptEngine>("engine", v)
            },
            { "engine-arg=", "Argument to pass to the JavaScript engine. Can be used more than once.",
                v => EngineArgs.Add(v)
            },
            { "error-patterns=|p=", "File containing error patterns. Each line prefixed with '@', or '%' for a simple string, or a .net regex, respectively.",
                v => ErrorPatternsFile = v
            },
            { "js-file=", "Main JavaScript file to be run on the JavaScript engine. Default is runtime.js",
                v => JSFile = v
            },
            { "expected-exit-code=", "If specified, sets the expected exit code for a successful instrumentation run.",
                v => {
                    if (int.TryParse(v, out var number))
                    {
                        ExpectedExitCode = number;
                        return;
                    }

                    throw new ArgumentException("expected-exit-code must be an integer");
                }
            },
        };

        public override void Validate()
        {
            base.Validate();
            _ = Engine;

            if (ErrorPatternsFile != null && !File.Exists(ErrorPatternsFile))
            {
                throw new ArgumentException($"Cannot find error patterns file {ErrorPatternsFile}");
            }
        }
    }
}
