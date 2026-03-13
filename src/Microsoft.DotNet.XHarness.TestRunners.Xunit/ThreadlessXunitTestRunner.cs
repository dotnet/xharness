// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.TestRunners.Common;
using Xunit;
using Xunit.Abstractions;

#nullable enable
namespace Microsoft.DotNet.XHarness.TestRunners.Xunit;

internal class ThreadlessXunitTestRunner : CustomXunitTestRunner
{
    public ThreadlessXunitTestRunner(LogWriter logger) : base(logger)
    {
    }

    protected override string RunnerDisplayName => "threadless Xunit runner";

    protected override string ResultsFileName { get => string.Empty; set => throw new InvalidOperationException("This runner outputs its results to stdout."); }

    protected override TestAssemblyConfiguration CreateConfiguration()
    {
        return new TestAssemblyConfiguration()
        {
            ShadowCopy = false,
            ParallelizeAssembly = false,
            ParallelizeTestCollections = false,
            MaxParallelThreads = 1,
            PreEnumerateTheories = false,
        };
    }

    public override async Task<string> WriteResultsToFile(XmlResultJargon xmlResultJargon)
    {
        Debug.Assert(xmlResultJargon == XmlResultJargon.xUnit);
        await WriteResultsToFile(Console.Out, xmlResultJargon);
        return "";
    }

    public override async Task WriteResultsToFile(TextWriter writer, XmlResultJargon jargon)
    {
        await WasmXmlResultWriter.WriteResultsToFile(ConsumeAssembliesElement());
    }
}

internal class ThreadlessXunitDiscoverer : global::Xunit.Sdk.XunitTestFrameworkDiscoverer
{
    public ThreadlessXunitDiscoverer(IAssemblyInfo assemblyInfo, ISourceInformationProvider sourceProvider, IMessageSink diagnosticMessageSink)
        : base(assemblyInfo, sourceProvider, diagnosticMessageSink)
    {
    }

    public void FindWithoutThreads(bool includeSourceInformation, IMessageSink discoveryMessageSink, ITestFrameworkDiscoveryOptions discoveryOptions)
    {
#pragma warning disable CS0618 // SynchronousMessageBus ctor is marked obsolete
        using (var messageBus = new global::Xunit.Sdk.SynchronousMessageBus(discoveryMessageSink))
#pragma warning restore
        {
            foreach (var type in AssemblyInfo.GetTypes(includePrivateTypes: false).Where(IsValidTestClass))
            {
                var testClass = CreateTestClass(type);
                if (!FindTestsForType(testClass, includeSourceInformation, messageBus, discoveryOptions))
                {
                    break;
                }
            }

            messageBus.QueueMessage(new global::Xunit.Sdk.DiscoveryCompleteMessage());
        }
    }
}

internal class ConsoleDiagnosticMessageSink(LogWriter logger) : global::Xunit.Sdk.LongLivedMarshalByRefObject, IMessageSink
{
    public bool OnMessage(IMessageSinkMessage message)
    {
        if (message is IDiagnosticMessage diagnosticMessage)
        {
            logger.OnDebug(diagnosticMessage.Message);
        }

        return true;
    }
}
