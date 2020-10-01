// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.Common.Logging
{
    internal class XHarnessConsoleLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentDictionary<string, XHarnessConsoleLogger> _loggers = new ConcurrentDictionary<string, XHarnessConsoleLogger>();
        private readonly XHarnessConsoleLoggerOptions _options;

        public XHarnessConsoleLoggerProvider(XHarnessConsoleLoggerOptions options)
        {
            _options = options;
        }

        public ILogger CreateLogger(string categoryName) =>
            _loggers.GetOrAdd(categoryName, loggerName => new XHarnessConsoleLogger(_options));

        public void Dispose() { }
    }
}
