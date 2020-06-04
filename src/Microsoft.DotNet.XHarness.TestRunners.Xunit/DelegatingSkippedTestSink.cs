using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.DotNet.XUnitExtensions;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.XHarness.TestRunners.Xunit
{
    /// <summary>
    /// A delegating implementation that will convert all failed tests due to the SkipTestException to be skipped
    /// tests rather than failed ones.
    /// </summary>
    public class DelegatingSkippedTestSink : LongLivedMarshalByRefObject, IExecutionSink
    {
       private readonly IExecutionSink _innerSink;
       private int _skipCount;

        public DelegatingSkippedTestSink(IExecutionSink innerSink)
            => this._innerSink = innerSink ?? throw new ArgumentNullException(nameof(innerSink));

        public ExecutionSummary ExecutionSummary => _innerSink.ExecutionSummary;

        public ManualResetEvent Finished => _innerSink.Finished;

        public void Dispose()
            => _innerSink.Dispose();

        public bool OnMessageWithTypes(IMessageSinkMessage message, HashSet<string> messageTypes)
        {
            var testFailed = message.Cast<ITestFailed>(messageTypes);
            if (testFailed != null)
            {
                // if we failed due to the SkipException, create a test skipped
                var exceptionType = testFailed.ExceptionTypes.FirstOrDefault();
                if (exceptionType == typeof(SkipTestException).FullName)
                {
                    _skipCount++;
                    var testSkipped = new TestSkipped(testFailed.Test, testFailed.Messages.FirstOrDefault());
                    return _innerSink.OnMessage(testSkipped);
                }
                else
                {
                    return _innerSink.OnMessage(testFailed);
                }
            }

            var testCollectionFinished = message.Cast<ITestCollectionFinished>(messageTypes);
            if (testCollectionFinished != null)
            {
                testCollectionFinished = new TestCollectionFinished(testCollectionFinished.TestCases,
                                                                    testCollectionFinished.TestCollection,
                                                                    testCollectionFinished.ExecutionTime,
                                                                    testCollectionFinished.TestsRun,
                                                                    testCollectionFinished.TestsFailed + testCollectionFinished.TestsSkipped,
                                                                    0);
                return _innerSink.OnMessage(testCollectionFinished);
            }

            var assemblyFinished = message.Cast<ITestAssemblyFinished>(messageTypes);
            if (assemblyFinished != null)
            {
                assemblyFinished = new TestAssemblyFinished(assemblyFinished.TestCases,
                                                            assemblyFinished.TestAssembly,
                                                            assemblyFinished.ExecutionTime,
                                                            assemblyFinished.TestsRun,
                                                            assemblyFinished.TestsFailed + assemblyFinished.TestsSkipped,
                                                            0);
                return _innerSink.OnMessage(assemblyFinished);
            }

            return _innerSink.OnMessageWithTypes(message, messageTypes);
        }
    }
}
