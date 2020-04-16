// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Hardware
{
    public class NoDeviceFoundException : Exception
    {
        public NoDeviceFoundException()
        {
        }

        public NoDeviceFoundException(string message) : base(message)
        {
        }

        public NoDeviceFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected NoDeviceFoundException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
