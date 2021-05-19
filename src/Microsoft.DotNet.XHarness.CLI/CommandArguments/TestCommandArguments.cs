// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments
{
    internal abstract class TestCommandArguments : AppRunCommandArguments
    {
        private readonly List<string> _singleMethodFilters = new();
        private readonly List<string> _classMethodFilters = new();

        /// <summary>
        /// Methods to be included in the test run while all others are ignored.
        /// </summary>
        public IEnumerable<string> SingleMethodFilters => _singleMethodFilters;

        /// <summary>
        /// Tests classes to be included in the run while all others are ignored.
        /// </summary>
        public IEnumerable<string> ClassMethodFilters => _classMethodFilters;
        public IList<(string path, string type)> WebServerMiddlewarePathsAndTypes { get; set; } = new List<(string, string)>();
        public IList<string> SetWebServerEnvironmentVariablesHttp { get; set; } = new List<string>();
        public IList<string> SetWebServerEnvironmentVariablesHttps { get; set; } = new List<string>();

        protected override OptionSet GetCommandOptions()
        {
            var options = base.GetCommandOptions();

            var testOptions = new OptionSet
            {
                {
                    "method|m=", "Method to be ran in the test application. When this parameter is used only the " +
                    "tests that have been provided by the '--method' and '--class' arguments will be ran. All other test will be " +
                    "ignored. Can be used more than once.",
                    v => _singleMethodFilters.Add(v)
                },
                {
                    "class|c=", "Test class to be ran in the test application. When this parameter is used only the " +
                    "tests that have been provided by the '--method' and '--class' arguments will be ran. All other test will be " +
                    "ignored. Can be used more than once.",
                    v => _classMethodFilters.Add(v)
                },
                { "web-server-middleware=", "<Path>,<typeName> to assembly and type which contains Kestrel middleware for local test server. Could be used multiple times to load multiple middlewares.",
                    v =>
                    {
                        var split = v.Split(',');
                        var file = split[0];
                        var type = split.Length > 1 && !string.IsNullOrWhiteSpace(split[1])
                                    ? split[1]
                                    : "GenericHandler";
                        if (string.IsNullOrWhiteSpace(file))
                        {
                            throw new ArgumentException($"Empty path to middleware assembly");
                        }
                        if (!File.Exists(file))
                        {
                            throw new ArgumentException($"Failed to find the middleware assembly at {file}");
                        }
                        WebServerMiddlewarePathsAndTypes.Add((file,type));
                    }
                },
                { "set-web-server-http-env=", "Comma separated list of environment variable names, which should be set to HTTP host and port, for the unit test, which use xharness as test web server.",
                    v => SetWebServerEnvironmentVariablesHttp = v.Split(',')
                },
                { "set-web-server-https-env=", "Comma separated list of environment variable names, which should be set to HTTPS host and port, for the unit test, which use xharness as test web server.",
                    v => SetWebServerEnvironmentVariablesHttps = v.Split(',')
                },
            };

            foreach (var option in testOptions)
            {
                options.Add(option);
            }

            foreach (var option in GetTestCommandOptions())
            {
                options.Add(option);
            }

            return options;
        }

        protected abstract OptionSet GetTestCommandOptions();
    }
}
