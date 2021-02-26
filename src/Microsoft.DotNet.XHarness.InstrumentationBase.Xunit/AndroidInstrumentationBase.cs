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
        private const string ExcludeCategoriesDirArgumentName = "exclude-categories-dir";
        private const string ExcludeCategoriesFileArgumentName = "exclude-categories-file";
        private const string ExcludeMethodArgumentName = "exclude-method";
        private const string IncludeMethodArgumentName = "include-method";
        private const string ExcludeClassArgumentName = "exclude-class";
        private const string IncludeClassArgumentName = "include-class";

        private Bundle? _bundle;

        protected Dictionary<string, string> bundleArguments = new Dictionary<string, string>();

        protected AndroidInstrumentationBase(IntPtr handle, JniHandleOwnership transfer)
            : base(handle, transfer)
        {
        }

        protected virtual IEnumerable<Assembly> Tests
        {
            get
            {
                yield return Assembly.GetExecutingAssembly();
            }
        }

        public override void OnCreate(Bundle? arguments)
        {
            base.OnCreate(arguments);

            _bundle = arguments;

            Start();
        }

        public override async void OnStart()
        {
            base.OnStart();

            var bundle = new Bundle();

            var entryPoint = new TestsEntryPoint(_bundle);
            entryPoint.TestsCompleted += (sender, results) =>
            {
                var message = String.Format("Tests run: {0} Passed: {1} Inconclusive: {2} Failed: {3} Ignored: {4}",
                results.SkippedTests, results.ExecutedTests, results.PassedTests, results.InconclusiveTests, results.FailedTests);

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
            private readonly string _resultsPath;
            private readonly string _excludeCategoriesDir;
            private readonly string _excludeCategoriesFile;
            private Dictionary<string, string> _parsedArguments = new Dictionary<string, string>();

            protected override string IgnoreFilesDirectory => _excludeCategoriesDir;
            protected override string IgnoredTraitsFilePath => _excludeCategoriesFile;

            public TestsEntryPoint(Bundle bundle)
            {
                var root = Application.Context.GetExternalFilesDir(null)?.AbsolutePath;

                if (root == null)
                {
                    throw new InvalidOperationException("Unable to retrieve tests results final path.");
                }

                var docsDir = Path.Combine(root, "Documents");

                if (!Directory.Exists(docsDir))
                {
                    Directory.CreateDirectory(docsDir);
                }

                if (bundle != null)
                {
                    foreach (var key in bundle.KeySet())
                    {
                        var value = bundle.GetString(key);
                        if (!string.IsNullOrEmpty(value))
                        {
                            _parsedArguments.Add(key, value);
                        }
                    }
                }

                // use default name for test results file
                _parsedArguments.TryAdd(ResultsFileArgumentName, "TestResults.xml");

                _resultsPath = Path.Combine(docsDir, _parsedArguments[ResultsFileArgumentName]);
                _excludeCategoriesDir = _parsedArguments[ExcludeCategoriesDirArgumentName];
                _excludeCategoriesFile = _parsedArguments[ExcludeCategoriesFileArgumentName];
            }

            protected override bool LogExcludedTests => true;

            public override TextWriter? Logger => null;

            public override string TestsResultsFinalPath => _resultsPath;

            protected override int? MaxParallelThreads => System.Environment.ProcessorCount/2;

            protected override IDevice? Device => null;

            public IEnumerable<Assembly> Tests { get; set; } = new List<Assembly>();
            protected override IEnumerable<TestAssemblyInfo> GetTestAssemblies()
            {
                foreach (var assembly in Tests)
                {
                    yield return new TestAssemblyInfo(assembly, assembly.Location);
                }
            }
            protected override void TerminateWithSuccess()
            {
            }

            private void ConfigureFilters(string? filter, Action<string, bool> filterMethod, bool isExcluded)
            {
                if (filter != null)
                {
                    foreach (var f in filter.Split(' '))
                    {
                        filterMethod(f, isExcluded);
                    }
                }
            }

            protected override TestRunner GetTestRunner(LogWriter logWriter)
            {
                var testRunner = base.GetTestRunner(logWriter);

                Tuple<string, Action<string, bool>, bool>[] filters =
                {
                    Tuple.Create<string, Action<string, bool>, bool>(_parsedArguments[ExcludeMethodArgumentName], testRunner.SkipMethod, true),
                    Tuple.Create<string, Action<string, bool>, bool>(_parsedArguments[ExcludeClassArgumentName], testRunner.SkipClass, true),
                    Tuple.Create<string, Action<string, bool>, bool>(_parsedArguments[IncludeMethodArgumentName], testRunner.SkipMethod, false),
                    Tuple.Create<string, Action<string, bool>, bool>(_parsedArguments[IncludeClassArgumentName], testRunner.SkipClass, false)
                };

                foreach (var t in filters)
                {
                    ConfigureFilters(t.Item1, t.Item2, t.Item3);
                }

                return testRunner;
            }
        }
    }
}
