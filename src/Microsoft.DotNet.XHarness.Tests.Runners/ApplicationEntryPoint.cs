// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Tests.Runners.Core;
using Microsoft.DotNet.XHarness.Tests.Runners.Xunit;

namespace Microsoft.DotNet.XHarness.Tests.Runners
{
    /// <summary>
    /// States the type of runner to be used by the application.
    /// </summary>
    public enum TestRunnerType
    {
        NUnit,
        Xunit,
    }

    /// <summary>
    /// Abstract class that represents the entry point of the test application.
    ///
    /// Subclasses must provide the minimum implementation to ensure that:
    ///
    /// Device: We do have the required device information for the logger.
    /// Assemblies: Provide a list of the assembly information to run.
    ///     assemblies can be loaded from disk or from memory, this is up to the
    ///     implementor.
    ///
    /// Clients that register to the class events and want to update the UI
    /// are responsible to do so in the main UI thread. The application entry
    /// point does not guarantee that the tests are executed in the ui thread.
    ///
    /// </summary>
    public abstract class ApplicationEntryPoint
    {
        const string ActiveIssueAttribute = "ActiveIssue";
        /// <summary>
        /// Event raised when the test run has started.
        /// </summary>
        public event EventHandler TestsStarted;

        /// <summary>
        /// Event raised when the test run has completed.
        /// </summary>
        public event EventHandler<TestRunResult> TestsCompleted;

        // fwd the events from the runner so that clients can connect to them

        /// <summary>
        /// Event raised when a test has started.
        /// </summary>
        public event EventHandler<string> TestStarted;

        /// <summary>
        /// Event raised when a test has completed or has been skipped.
        /// </summary>
        public event EventHandler<(string TestName, TestResult TestResult)> TestCompleted;

        protected abstract int? MaxParallelThreads { get; }

        /// <summary>
        /// Must be implemented and return a class that returns the information
        /// of a device. It can return null.
        /// </summary>
        protected abstract IDevice Device { get; }

        /// <summary>
        /// Returns the IEnumerable with the asseblies that contain the tests
        /// to be ran.
        /// </summary>
        /// <returns></returns>
        protected abstract IEnumerable<TestAssemblyInfo> GetTestAssemblies();

        /// <summary>
        /// Returns the type of runner to use.
        /// </summary>
        protected abstract TestRunnerType TestRunner { get; }

        /// <summary>
        /// Returns the directory that contains the ignore files.
        /// </summary>
        protected abstract string IgnoreFilesDirectory { get; }

        /// <summary>
        /// Terminates the application. This should ensure that it is executed
        /// in the main thread.
        /// </summary>
        protected abstract void TerminateWithSuccess();

        /// <summary>
        /// Execute the tests in an async mode.
        /// </summary>
        public abstract Task RunAsync();

        /// <summary>
        /// Get/Set the minimun log level to be used by the runner logging.
        /// </summary>
        public MinimumLogLevel MinimumLogLevel { get; set; } = MinimumLogLevel.Info;

        void OnTestStarted(object sender, string testName)
        {
            var handler = TestStarted;
            if (handler != null)
                handler(sender, testName);
        }

        void OnTestCompleted(object sender, (string TestName, TestResult Testresult) result)
        {
            var handler = TestCompleted;
            if (handler != null)
                handler(sender, result);
        }

        protected async Task<TestRunner> InternalRunAsync (LogWriter logger)
        {
            logger.MinimumLogLevel = MinimumLogLevel;
            TestRunner runner;
            switch (TestRunner)
            {
                case TestRunnerType.NUnit:
                    throw new NotImplementedException();
                default:
                    runner = new XUnitTestRunner(logger)
                    {
                        MaxParallelThreads = MaxParallelThreads
                    };
                    break;
            }
            // connect to the runner events so that we fwd them to the client
            runner.TestStarted += OnTestStarted;
            runner.TestCompleted += OnTestCompleted;
            var categories = new List<string> { }; // default known category to ignore

            if (!string.IsNullOrEmpty(IgnoreFilesDirectory))
            {
                categories.AddRange (await IgnoreFileParser.ParseTraitsContentFileAsync(IgnoreFilesDirectory, TestRunner == TestRunnerType.Xunit));
                // add category filters if they have been added

                var skippedTests = await IgnoreFileParser.ParseContentFilesAsync(IgnoreFilesDirectory);
                if (skippedTests.Any())
                {
                    // ensure that we skip those tests that have been passed via the ignore files
                    runner.SkipTests(skippedTests);
                }
            }
            runner.SkipCategories(categories);

            var attributes = new List<string> {ActiveIssueAttribute}; // known attrs to skip
            runner.SkipAttributes(attributes);

            var testAssemblies = GetTestAssemblies();
            // notify the clients we are starting
            TestsStarted?.Invoke(this, new EventArgs());

            await runner.Run(testAssemblies).ConfigureAwait(false);

            var result = new TestRunResult(runner);
            // notify the client we are done and the results, but do not expose
            // the runner.
            TestsCompleted?.Invoke(this, result);
            return runner;
        }
    }
}
