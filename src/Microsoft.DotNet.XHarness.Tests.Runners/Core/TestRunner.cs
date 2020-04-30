// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;


namespace Microsoft.DotNet.XHarness.Tests.Runners.Core
{
    public abstract class TestRunner
    {
        public enum Jargon
        {
            TouchUnit,
            NUnitV2,
            NUnitV3,
            xUnit,
        }

        /// <summary>
        /// Event raised when a test has started.
        /// </summary>
        public event EventHandler<string> TestStarted;

        /// <summary>
        /// Event raised when a test has completed or has been skipped.
        /// </summary>
        public event EventHandler<(string TestName, TestResult TestResult)> TestCompleted;

        /// <summary>
        /// Number of inconclusive tests.
        /// </summary>
        public long InconclusiveTests { get; protected set; } = 0;

        /// <summary>
        /// Number of failed tests.
        /// </summary>
        public long FailedTests { get; protected set; } = 0;

        /// <summary>
        /// Number of successful tests.
        /// </summary>
        public long PassedTests { get; protected set; } = 0;

        /// <summary>
        /// Number of skipped tests.
        /// </summary>
        public long SkippedTests { get; protected set; } = 0;

        /// <summary>
        /// Number of executed tests, which can be the same of lower thant the
        /// number of total tests.
        /// </summary>
        public long ExecutedTests { get; protected set; } = 0;

        /// <summary>
        /// Total number of tests. This icludes, executed, ignored, filtered 
		/// and skipped tests.
        /// </summary>
        public long TotalTests { get; protected set; } = 0;

        /// <summary>
        /// Number of tests that were not executed because they matched a
		/// filter.
        /// </summary>
        public long FilteredTests { get; protected set; } = 0;

        /// <summary>
        /// Specify if the runner should execute tests in parallel. Default is
        /// false.
        /// </summary>
        public bool RunInParallel { get; set; } = false;

        /// <summary>
        /// Root directory of the tests.
        /// </summary>
        public string TestsRootDirectory { get; set; }

        /// <summary>
        /// Specify if all the tests found in the assemblies should be ran.
        /// Default is true.
        /// </summary>
        public bool RunAllTestsByDefault { get; set; } = true;

        /// <summary>
        /// Specify if the runner should log those tests that have been excluded
        /// due to a filter. Default is false. 
        /// </summary>
        public bool LogExcludedTests { get; set; }

        /// <summary>
        /// TextWriter that will be used to write the results of the test run.
        /// </summary>
        public TextWriter Writer { get; set; }

        /// <summary>
        /// List that contains all the failured that occured in the test run.
        /// </summary>
        public List<TestFailureInfo> FailureInfos { get; } = new List<TestFailureInfo>();

        /// <summary>
        /// Logging object.
        /// </summary>
        protected LogWriter Logger { get; }

        /// <summary>
        /// Name of the file that will be used to write the results.
        /// </summary>
        protected abstract string ResultsFileName { get; set; }

        protected TestRunner(LogWriter logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public abstract Task Run(IEnumerable<TestAssemblyInfo> testAssemblies);
        public abstract string WriteResultsToFile(Jargon jargon);
        public abstract void WriteResultsToFile(TextWriter writer, Jargon jargon);
        public abstract void SkipTests(IEnumerable<string> tests);
        public abstract void SkipCategories(IEnumerable<string> categories);

        public abstract void SkipAttributes(IEnumerable<string> attributes);

        protected void OnError(string message)
        {
            Logger.OnError(message);
        }

        protected void OnWarning(string message)
        {
            Logger.OnWarning(message);
        }

        protected void OnDebug(string message)
        {
            Logger.OnDebug(message);
        }

        protected void OnDiagnostic(string message)
        {
            Logger.OnDiagnostic(message);
        }

        protected void OnInfo(string message)
        {
            Logger.OnInfo(message);
        }

        protected void OnAssemblyStart(Assembly asm)
        {
        }

        protected void OnAssemblyFinish(Assembly asm)
        {
        }

        protected void LogFailureSummary()
        {
            if (FailureInfos == null || FailureInfos.Count == 0)
                return;

            OnInfo("Failed tests:");
            for (int i = 1; i <= FailureInfos.Count; i++)
            {
                TestFailureInfo info = FailureInfos[i - 1];
                if (info == null || !info.HasInfo)
                    continue;

                OnInfo($"{i}) {info.Message}");
            }
        }

        void AssertExecutionState(TestExecutionState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
        }

        protected virtual string GetResultsFilePath()
        {
            if (String.IsNullOrEmpty(ResultsFileName))
                throw new InvalidOperationException("Runner didn't specify a valid results file name");


            string resultsPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!Directory.Exists(resultsPath))
                Directory.CreateDirectory(resultsPath);
            return Path.Combine(resultsPath, ResultsFileName);
        }

        protected virtual void OnTestStarted(string testName)
        {
            var hanlder = TestStarted;
            if (hanlder != null)
                hanlder(this, testName);
        }

        protected virtual void OnTestCompleted((string TestName, TestResult TestResult) result)
        {
            var handler = TestCompleted;
            if (handler != null)
                handler(this, result); 
		}
    }
}
