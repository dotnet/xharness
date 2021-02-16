using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Android.App;
using Android.OS;
using Android.Runtime;
using Microsoft.DotNet.XHarness.TestRunners.Common;
using Microsoft.DotNet.XHarness.TestRunners.Xunit;

// parsing of arguments for which test methods to run
// properly set the return-code
// to do the filter and excludes
// automatically return the results file

namespace Microsoft.DotNet.XHarness.InstrumentationBase.Xunit
{
    public abstract class AndroidInstrumentationBase : Instrumentation
    {
        private const string ResultsFileArgumentName = "results-file-name";
        private const string ResultsPathArgumentName = "test-results-path";
        private const string SummaryMessageArgumentName = "test-execution-summary";
        private const string ExitCodeArgumentName = "return-code";
        private const string ExcludeCategoriesArgumentName = "exclude-categories";
        private const string ExcludeMethodArgumentName = "exclude-method";
        private const string IncludeMethodArgumentName = "include-method";
        private const string ExcludeClassArgumentName = "exclude-class";
        private const string IncludeClassArgumentName = "include-class";

        protected Dictionary<string, string> bundleArguments;

        protected AndroidInstrumentationBase()
        {
        }
        protected AndroidInstrumentationBase(IntPtr handle, JniHandleOwnership transfer)
            : base(handle, transfer)
        {
        }

        protected virtual IEnumerable<string> Tests
        {
            get
            {
                yield return Assembly.GetExecutingAssembly().Location;
            }
        }

        public override void OnCreate(Bundle arguments)
        {
            base.OnCreate(arguments);

            foreach (var key in arguments.KeySet())
            {
                string value = arguments.GetString(key);
                if (!string.IsNullOrEmpty(value))
                {
                    bundleArguments.Add(key, value);
                }
            }

            if (bundleArguments.ContainsKey(ResultsFileArgumentName) == false)
            {
                bundleArguments.Add(ResultsFileArgumentName, "TestResults.xml");
            }

            Start();
        }

        public override async void OnStart()
        {
            base.OnStart();

            var bundle = new Bundle();

            var entryPoint = new TestsEntryPoint(bundleArguments);
            entryPoint.TestsCompleted += (sender, results) =>
            {
                var message =
                    $"Tests run: {results.ExecutedTests} " +
                    $"Passed: {results.PassedTests} " +
                    $"Inconclusive: {results.InconclusiveTests} " +
                    $"Failed: {results.FailedTests} " +
                    $"Ignored: {results.SkippedTests}";
                bundle.PutString(SummaryMessageArgumentName, message);
                
                bundle.PutLong(ExitCodeArgumentName, results.FailedTests == 0 ? 0 : 1);
            };

            entryPoint.Tests = Tests;

            await entryPoint.RunAsync();

            if (File.Exists(entryPoint.TestsResultsFinalPath))
            {
                bundle.PutString(ResultsPathArgumentName, entryPoint.TestsResultsFinalPath);
            }
            
            if (bundle.GetLong(ExitCodeArgumentName, -1) == -1)
            {
                bundle.PutLong(ExitCodeArgumentName, 1);
            }

            Finish(Result.Ok, bundle);
        }

        class TestsEntryPoint : AndroidApplicationEntryPoint
        {
            readonly string resultsPath;
            readonly string includeMethod;
            readonly string excludeCategories;
            readonly string excludeMethod;
            readonly string includeClass;
            readonly string excludeClass;

            public TestsEntryPoint(Dictionary<string, string> arguments)
            {
                var root = Application.Context.GetExternalFilesDir(null)?.AbsolutePath;

                var docsDir = Path.Combine(root, "Documents");

                if (!Directory.Exists(docsDir))
                {
                    Directory.CreateDirectory(docsDir);
                }

                resultsPath = Path.Combine(docsDir, arguments[ResultsFileArgumentName]);
                includeMethod = arguments[IncludeMethodArgumentName];
                excludeCategories = arguments[ExcludeCategoriesArgumentName];
                excludeMethod = arguments[ExcludeMethodArgumentName];
                includeClass = arguments[IncludeClassArgumentName];
                excludeClass = arguments[ExcludeClassArgumentName];
            }

            protected override bool LogExcludedTests => true;

            public override TextWriter Logger => null;

            public override string TestsResultsFinalPath => resultsPath;

            protected override int? MaxParallelThreads => System.Environment.ProcessorCount;

            protected override IDevice Device => null;

            public IEnumerable<string> Tests { get; internal set; }

            protected override IEnumerable<TestAssemblyInfo> GetTestAssemblies()
            {
                //yield return new TestAssemblyInfo(Assembly.GetExecutingAssembly(), Assembly.GetExecutingAssembly().Location);
                //yield return new TestAssemblyInfo(typeof(Battery_Tests).Assembly, typeof(Battery_Tests).Assembly.Location);
                foreach (string file in Tests)
                {
                    yield return new TestAssemblyInfo(Assembly.LoadFrom(file), file);
                }
            }
            protected override void TerminateWithSuccess()
            {
            }

            protected override TestRunner GetTestRunner(LogWriter logWriter)
            {
                var testRunner = base.GetTestRunner(logWriter);
                if (excludeCategories != null)
                {
                    testRunner.SkipCategories(excludeCategories.Split(' '));
                }
                if (excludeMethod != null)
                {
                    foreach (var method in excludeMethod.Split(' '))
                    {
                        testRunner.SkipMethod(method, true);
                    }
                }
                if (excludeClass != null)
                {
                    foreach (var item in excludeClass.Split(' '))
                    {
                        testRunner.SkipMethod(item, true);
                    }
                }
                if (includeMethod != null)
                {
                    foreach (var method in includeMethod.Split(' '))
                    {
                        testRunner.SkipMethod(method, false);
                    }
                }
                if (includeClass != null)
                {
                    foreach (var item in includeClass.Split(' '))
                    {
                        testRunner.SkipMethod(item, false);
                    }
                }
                return testRunner;
            }
        }
    }
}
