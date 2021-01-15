// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm
{
    /// <summary>
    /// Specifies the name of a Browser used to run the WASM application
    /// </summary>
    internal enum Browser
    {
        /// <summary>
        /// Chrome
        /// </summary>
        Chrome,

        /// <summary>
        /// Safari
        /// </summary>
        Safari,

        /// <summary>
        /// Firefox
        /// </summary>
        Firefox
    }

    internal class WasmTestBrowserCommandArguments : TestCommandArguments
    {
        public Browser Browser { get; set; } = Browser.Chrome;

        public List<string> BrowserArgs { get; set; } = new List<string>();
        public string HTMLFile { get; set; } = "index.html";

        protected override OptionSet GetTestCommandOptions() => new OptionSet{
            { "browser=|b=", "Specifies the browser to be used",
                v => Browser = ParseArgument<Browser>("browser", v)
            },
            { "browser-arg=", "Argument to pass to the browser. Can be used more than once.",
                v => BrowserArgs.Add(v)
            },
            { "html-file=", "Main html file to load from the app directory. Default is index.html",
                v => HTMLFile = v
            },
        };

        public override void Validate()
        {
            base.Validate();

            if (Browser == Browser.Safari && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                throw new ArgumentException("Safari is only supported on OSX");
            }

            if (!Directory.Exists(AppPackagePath))
            {
                throw new ArgumentException($"Failed to find the app bundle at {AppPackagePath}");
            }

            if (Path.IsPathRooted (HTMLFile))
            {
                throw new ArgumentException("--html-file argument must be a relative path");
            }
        }
    }
}
