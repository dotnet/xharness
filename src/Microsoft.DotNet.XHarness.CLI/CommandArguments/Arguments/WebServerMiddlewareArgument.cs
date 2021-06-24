// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments
{
    internal class WebServerMiddlewareArgument : Argument<List<(string path, string type)>>
    {
        public WebServerMiddlewareArgument()
            : base(
                  "web-server-middleware=",
                  "<path>,<typeName> to assembly and type which contains Kestrel middleware for local test server. Could be used multiple times to load multiple middlewares",
                  new List<(string path, string type)>())
        {
        }

        public override void Action(string argumentValue)
        {
            var split = argumentValue.Split(',');
            var file = split[0];
            var type = split.Length > 1 && !string.IsNullOrWhiteSpace(split[1]) ? split[1] : "GenericHandler";

            Value.Add((file, type));
        }

        public override void Validate()
        {
            base.Validate();

            foreach (var (path, type) in Value)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    throw new ArgumentException($"Empty path to middleware assembly");
                }

                if (!File.Exists(path))
                {
                    throw new ArgumentException($"Failed to find the middleware assembly at {path}");
                }
            }
        }
    }
}
