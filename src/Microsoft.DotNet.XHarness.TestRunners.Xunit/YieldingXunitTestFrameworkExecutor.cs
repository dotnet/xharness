// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

public class YieldingXunit2 : Xunit2
{
    readonly YieldingXunitTestFrameworkExecutor remoteExecutor;

    public YieldingXunit2(AppDomainSupport appDomainSupport,
                  ISourceInformationProvider sourceInformationProvider,
                  string assemblyFileName,
                  string configFileName = null,
                  bool shadowCopy = true,
                  string shadowCopyFolder = null,
                  IMessageSink diagnosticMessageSink = null,
                  bool verifyTestAssemblyExists = true)
        : base(appDomainSupport, sourceInformationProvider, assemblyFileName, configFileName, shadowCopy, shadowCopyFolder, diagnosticMessageSink, verifyTestAssemblyExists)
    {
        var an = Assembly.Load(new AssemblyName { Name = Path.GetFileNameWithoutExtension(assemblyFileName) }).GetName();
        var assemblyName = new AssemblyName { Name = an.Name, Version = an.Version };
        //remoteExecutor = Framework.GetExecutor(assemblyName);
        remoteExecutor = new YieldingXunitTestFrameworkExecutor(assemblyName, NullSourceInformationProvider.Instance, DiagnosticMessageSink);
    }

    public async Task RunTestsAsync(IEnumerable<ITestCase> testCases, IMessageSink messageSink, ITestFrameworkExecutionOptions executionOptions)
    {
        await remoteExecutor.RunTestCasesAsync(testCases.Cast<IXunitTestCase>(), CreateOptimizedRemoteMessageSink(messageSink), executionOptions);
    }
}

internal class NullSourceInformationProvider : ISourceInformationProvider
{
    public static readonly NullSourceInformationProvider Instance = new NullSourceInformationProvider();

    public ISourceInformation GetSourceInformation(ITestCase testCase)
    {
        return new Xunit.SourceInformation();
    }

    public void Dispose() { }
}

internal class YieldingXunitTestFrameworkExecutor : XunitTestFrameworkExecutor
{
    public YieldingXunitTestFrameworkExecutor(AssemblyName assemblyName, ISourceInformationProvider sourceInformationProvider, IMessageSink diagnosticMessageSink)
        : base(assemblyName, sourceInformationProvider, diagnosticMessageSink)
    {
    }

    public async Task RunTestCasesAsync(IEnumerable<IXunitTestCase> testCases, IMessageSink executionMessageSink, ITestFrameworkExecutionOptions executionOptions)
    {
        using (var assemblyRunner = new YieldingXunitTestAssemblyRunner(TestAssembly, testCases, DiagnosticMessageSink, executionMessageSink, executionOptions))
            await assemblyRunner.RunAsync();
    }

    protected override async void RunTestCases(IEnumerable<IXunitTestCase> testCases, IMessageSink executionMessageSink, ITestFrameworkExecutionOptions executionOptions)
    {
        await RunTestCasesAsync(testCases, executionMessageSink, executionOptions);
    }
}

internal class YieldingXunitTestAssemblyRunner : XunitTestAssemblyRunner
{
    public YieldingXunitTestAssemblyRunner(ITestAssembly testAssembly, IEnumerable<IXunitTestCase> testCases, IMessageSink diagnosticMessageSink, IMessageSink executionMessageSink, ITestFrameworkExecutionOptions executionOptions)
        : base(testAssembly, testCases, diagnosticMessageSink, executionMessageSink, executionOptions)
    {
    }

    /// <inheritdoc/>
    protected override async Task<RunSummary> RunTestCollectionAsync(IMessageBus messageBus, ITestCollection testCollection, IEnumerable<IXunitTestCase> testCases, CancellationTokenSource cancellationTokenSource)
    {
        await Task.Yield();
        return await new YieldingXunitTestCollectionRunner(testCollection, testCases, DiagnosticMessageSink, messageBus, TestCaseOrderer, new ExceptionAggregator(Aggregator), cancellationTokenSource).RunAsync();
    }


    protected override async Task<RunSummary> RunTestCollectionsAsync(IMessageBus messageBus, CancellationTokenSource cancellationTokenSource)
    {
        await Task.Yield();
        return await base.RunTestCollectionsAsync(messageBus, cancellationTokenSource);
    }
}

internal class YieldingXunitTestCollectionRunner : XunitTestCollectionRunner
{
    public YieldingXunitTestCollectionRunner(ITestCollection testCollection, IEnumerable<IXunitTestCase> testCases, IMessageSink diagnosticMessageSink, IMessageBus messageBus, ITestCaseOrderer testCaseOrderer, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource) : base(testCollection, testCases, diagnosticMessageSink, messageBus, testCaseOrderer, aggregator, cancellationTokenSource)
    {
    }

    protected override async Task<RunSummary> RunTestClassAsync(ITestClass testClass, IReflectionTypeInfo @class, IEnumerable<IXunitTestCase> testCases)
    {
        await Task.Yield();
        var x = new YieldingXunitTestClassRunner(testClass, @class, testCases, DiagnosticMessageSink, MessageBus, TestCaseOrderer, new ExceptionAggregator(Aggregator), CancellationTokenSource, CollectionFixtureMappings);
        return await x.RunAsync();
    }
}

internal class YieldingXunitTestClassRunner : XunitTestClassRunner
{
    public YieldingXunitTestClassRunner(ITestClass testClass, IReflectionTypeInfo @class, IEnumerable<IXunitTestCase> testCases, IMessageSink diagnosticMessageSink, IMessageBus messageBus, ITestCaseOrderer testCaseOrderer, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource, IDictionary<Type, object> collectionFixtureMappings)
        : base(testClass, @class, testCases, diagnosticMessageSink, messageBus, testCaseOrderer, aggregator, cancellationTokenSource, collectionFixtureMappings)
    {
    }

    protected override async Task<RunSummary> RunTestMethodAsync(ITestMethod testMethod, IReflectionMethodInfo method, IEnumerable<IXunitTestCase> testCases, object[] constructorArguments)
    {
        await Task.Yield();
        return await base.RunTestMethodAsync(testMethod, method, testCases, constructorArguments);
    }
}
