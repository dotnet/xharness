// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Logging
{
    /// <summary>
    /// Log that only writes to memory
    /// </summary>
    public class MemoryLog : ReadableLog
    {
        readonly StringBuilder captured = new StringBuilder();

        protected override void WriteImpl(string value)
        {
            captured.Append(value);
        }

        public override StreamReader GetReader()
        {
            var str = new MemoryStream(Encoding.GetBytes(captured.ToString()));
            return new StreamReader(str, Encoding, false);
        }

        public override void Flush()
        {
        }

        public override void Dispose()
        {
        }

        public override string ToString() => captured.ToString();
    }
}
