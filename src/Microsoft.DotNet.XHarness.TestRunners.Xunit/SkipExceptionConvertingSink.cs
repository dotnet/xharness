// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Xunit.Abstractions;

#nullable enable
namespace Microsoft.DotNet.XHarness.TestRunners.Xunit;

/// <summary>
/// A message sink wrapper that intercepts <see cref="ITestFailed"/> messages caused by
/// <c>Microsoft.DotNet.XUnitExtensions.SkipTestException</c> and converts them to
/// <see cref="ITestSkipped"/> messages so the test is reported as skipped rather than failed.
/// </summary>
internal class SkipExceptionConvertingSink : IMessageSink
{
    private const string SkipTestExceptionTypeName = "Microsoft.DotNet.XUnitExtensions.SkipTestException";

    private readonly IMessageSink _innerSink;

    public SkipExceptionConvertingSink(IMessageSink innerSink)
    {
        _innerSink = innerSink;
    }

    public bool OnMessage(IMessageSinkMessage message)
    {
        if (message is ITestFailed testFailed &&
            testFailed.ExceptionTypes != null &&
            testFailed.ExceptionTypes.Any(t => t == SkipTestExceptionTypeName))
        {
            var reason = testFailed.Messages != null && testFailed.Messages.Length > 0
                ? testFailed.Messages[0]
                : "Test skipped via SkipTestException";
            var skippedMessage = new global::Xunit.Sdk.TestSkipped(testFailed.Test, reason);
            return _innerSink.OnMessage(skippedMessage);
        }

        return _innerSink.OnMessage(message);
    }
}
