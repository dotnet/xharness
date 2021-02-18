using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Android.App;
using Android.OS;
using Android.Runtime;
using Microsoft.DotNet.XHarness.Common.CLI;
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

            // use default name for test results file
            bundleArguments.TryAdd(ResultsFileArgumentName, "TestResults.xml");

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
                
                bundle.PutLong(ExitCodeArgumentName, (long)(results.FailedTests == 0 ? ExitCode.SUCCESS : ExitCode.TESTS_FAILED));
            };

            entryPoint.Tests = Tests;

            await entryPoint.RunAsync();

            if (File.Exists(entryPoint.TestsResultsFinalPath))
            {
                bundle.PutString(ResultsPathArgumentName, entryPoint.TestsResultsFinalPath);
            }
            
            if (bundle.GetLong(ExitCodeArgumentName, 82) == (long)ExitCode.RETURN_CODE_NOT_SET)
            {
                bundle.PutLong(ExitCodeArgumentName, (long)ExitCode.RETURN_CODE_NOT_SET);
            }

            Finish(Result.Ok, bundle);
        }

        class TestsEntryPoint : AndroidApplicationEntryPoint
        {
            readonly string _resultsPath;
            readonly string _includeMethod;
            readonly string _excludeCategories;
            readonly string _excludeMethod;
            readonly string _includeClass;
            readonly string _excludeClass;

            public TestsEntryPoint(Dictionary<string, string> arguments)
            {
                var root = Application.Context.GetExternalFilesDir(null)?.AbsolutePath;

                var docsDir = Path.Combine(root, "Documents");

                if (!Directory.Exists(docsDir))
                {
                    Directory.CreateDirectory(docsDir);
                }

                _resultsPath = Path.Combine(docsDir, arguments[ResultsFileArgumentName]);
                _includeMethod = arguments[IncludeMethodArgumentName];
                _excludeCategories = arguments[ExcludeCategoriesArgumentName];
                _excludeMethod = arguments[ExcludeMethodArgumentName];
                _includeClass = arguments[IncludeClassArgumentName];
                _excludeClass = arguments[ExcludeClassArgumentName];
            }

            protected override bool LogExcludedTests => true;

            public override TextWriter Logger => null;

            public override string TestsResultsFinalPath => _resultsPath;

            protected override int? MaxParallelThreads => System.Environment.ProcessorCount;

            protected override IDevice Device => null;

            public IEnumerable<string> Tests { get; internal set; }

            protected override IEnumerable<TestAssemblyInfo> GetTestAssemblies()
            {
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
                if (_excludeCategories != null)
                {
                    testRunner.SkipCategories(_excludeCategories.Split(' '));
                }
                if (_excludeMethod != null)
                {
                    testRunner.RunAllTestsByDefault = false;
                    foreach (var method in _excludeMethod.Split(' '))
                    {
                        testRunner.SkipMethod(method, true);
                    }
                }
                if (_excludeClass != null)
                {
                    testRunner.RunAllTestsByDefault = false;
                    foreach (var item in _excludeClass.Split(' '))
                    {
                        testRunner.SkipMethod(item, true);
                    }
                }
                if (_includeMethod != null)
                {
                    foreach (var method in _includeMethod.Split(' '))
                    {
                        testRunner.SkipMethod(method, false);
                    }
                }
                if (_includeClass != null)
                {
                    foreach (var item in _includeClass.Split(' '))
                    {
                        testRunner.SkipMethod(item, false);
                    }
                }
                return testRunner;
            }
        }
    }
}
