﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.DotNet.XHarness.Android;

public class AdbFailureException : Exception
{
    public AdbFailureException(string message) : base(message)
    {
    }
}
