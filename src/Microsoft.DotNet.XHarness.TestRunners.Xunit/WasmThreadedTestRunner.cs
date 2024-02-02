// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.TestRunners.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.XHarness.TestRunners.Xunit;

internal class WasmThreadedTestRunner : XUnitTestRunner
{
    public WasmThreadedTestRunner(LogWriter logger) : base(logger)
    {
        TestStagePrefix = string.Empty;
    }

    protected override string ResultsFileName { get => string.Empty; set => throw new InvalidOperationException("This runner outputs its results to stdout."); }

    protected override void HandleTestFailed(ITestFailed msg)
    {
        OnError($"[FAIL] {WasmXmlResultWriter.EscapeNewLines(msg.Test.DisplayName)}{Environment.NewLine}{ExceptionUtility.CombineMessages(msg)}{Environment.NewLine}{ExceptionUtility.CombineStackTraces(msg)}");
        FailedTests++;
    }

    public override void WriteResultsToFile(TextWriter writer, XmlResultJargon jargon)
        => WasmXmlResultWriter.WriteOnSingleLine(AssembliesElement);
}
