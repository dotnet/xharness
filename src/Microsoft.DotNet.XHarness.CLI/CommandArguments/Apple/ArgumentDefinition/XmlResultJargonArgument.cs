﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple
{
    /// <summary>
    /// Allows to specify the xml format to be used in the result files.
    /// </summary>
    internal class XmlResultJargonArgument : ArgumentDefinition
    {
        public XmlResultJargon Value { get; private set; } = XmlResultJargon.xUnit;

        public XmlResultJargonArgument()
            : base("xml-jargon=|xj=", $"The xml format to be used in the unit test results. Can be {XmlResultJargon.TouchUnit}, {XmlResultJargon.NUnitV2}, {XmlResultJargon.NUnitV3} or {XmlResultJargon.xUnit}.")
        {
        }

        public override void Action(string argumentValue)
        {
            Value = ParseArgument("xml-jargon", argumentValue, invalidValues: XmlResultJargon.Missing);
        }
    }
}
