// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Logging
{
    // A log that forwards all written data to a callback
    public class CallbackLog : Log
    {
        private readonly Action<string> _onWrite;

        public CallbackLog(Action<string> onWrite)
            : base("Callback log")
        {
            _onWrite = onWrite;
        }

        public override void Dispose()
        {
        }

        public override void Flush()
        {
        }

        protected override void WriteImpl(string value) => _onWrite(value);
    }
}
