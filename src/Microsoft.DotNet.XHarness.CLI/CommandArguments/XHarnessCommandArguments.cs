// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments
{
    internal abstract class XHarnessCommandArguments : ICommandArguments
    {
        public LogLevel Verbosity { get; set; }

        public abstract IList<string> GetValidationErrors();

        protected static string RootPath(string path)
        {
            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(Directory.GetCurrentDirectory(), path);
            }

            return path;
        }
    }
}
