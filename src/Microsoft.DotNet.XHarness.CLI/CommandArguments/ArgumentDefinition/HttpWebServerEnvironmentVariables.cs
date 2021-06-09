﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments
{
    internal class HttpWebServerEnvironmentVariables : ArgumentDefinition<IEnumerable<string>>
    {
        public HttpWebServerEnvironmentVariables()
            : base("set-web-server-http-env=", "Comma separated list of environment variable names, which should be set to HTTP host and port, for the unit test, which use xharness as test web server")
        {
        }

        public override void Action(string argumentValue) => Value = argumentValue.Split(',');
    }
}
