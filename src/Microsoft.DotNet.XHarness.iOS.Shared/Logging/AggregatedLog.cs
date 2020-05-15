// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.XHarness.Common.Logging;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared.Logging
{

    public abstract partial class Log
    {

        public static ILog CreateAggregatedLogWithDefault(ILog defaultLog, params ILog[] logs)
        {
            return new AggregatedLog(defaultLog, logs);
        }

        [Obsolete ("AggregatedLogs without a default log are dangerous. Use 'CreateAggregatedLogWithDefault' instead.")]
        public static ILog CreateAggregatedLog(params ILog[] logs)
        {
            return new AggregatedLog(logs);
        }

        // Log that will duplicate log output to multiple other logs.
        class AggregatedLog : Log
        {
            readonly ILog? _defaultLog;
            readonly List<ILog> _logs = new List<ILog>();

            public AggregatedLog(ILog defaultLog, params ILog[] logs)
            {
                _defaultLog = defaultLog ?? throw new ArgumentNullException(nameof(defaultLog));
                _logs.Add(defaultLog);
                _logs.AddRange(logs);
            }

            public AggregatedLog(params ILog[] logs)
                : base(null)
            {
                _logs.AddRange(logs);
            }

            public override string FullPath
            {
                get
                {
                    if (_defaultLog == null)
                        throw new InvalidOperationException("Default log not set.");
                    return _defaultLog.FullPath;
                }
            }

            protected override void WriteImpl(string value)
            {
                foreach (var log in _logs)
                    log.Write(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                foreach (var log in _logs)
                    log.Write(buffer, offset, count);
            }

            public override StreamReader GetReader()
            {
                if (_defaultLog == null)
                    throw new InvalidOperationException("Default log not set.");
                return _defaultLog.GetReader();
            }

            public override void Flush()
            {
                foreach (var log in _logs)
                    log.Flush();
            }

            public override void Dispose()
            {
                foreach (var log in _logs)
                    log.Dispose();
            }
        }
    }
}
