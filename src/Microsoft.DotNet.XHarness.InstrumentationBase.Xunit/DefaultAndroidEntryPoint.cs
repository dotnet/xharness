using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.DotNet.XHarness.TestRunners.Common;
using Microsoft.DotNet.XHarness.TestRunners.Xunit;

namespace Microsoft.DotNet.XHarness.DefaultAndroidEntryPoint.Xunit
{
    public class DefaultAndroidEntryPoint : AndroidApplicationEntryPoint
    {
        private const string ResultsFileArgumentName = "results-file-name";
        private const string ResultsFileArgumentPath = "results-file-path";
        private const string ExcludeCategoriesDirArgumentName = "exclude-categories-dir";
        private const string ExcludeCategoriesFileArgumentName = "exclude-categories-file";
        private const string ExcludeMethodArgumentName = "exclude-method";
        private const string IncludeMethodArgumentName = "include-method";
        private const string ExcludeClassArgumentName = "exclude-class";
        private const string IncludeClassArgumentName = "include-class";
        private readonly string _resultsPath;
        private readonly string _excludeCategoriesDir;
        private readonly string _excludeCategoriesFile;
        private readonly Dictionary<string, string> _parsedArguments;

        protected override string IgnoreFilesDirectory => _excludeCategoriesDir;
        protected override string IgnoredTraitsFilePath => _excludeCategoriesFile;

        public DefaultAndroidEntryPoint(Dictionary<string, string> bundle)
        {
            _parsedArguments = bundle;

            // use default name for test results file
            _parsedArguments.TryAdd(ResultsFileArgumentName, "TestResults.xml");

            if (!Directory.Exists(_parsedArguments[ResultsFileArgumentPath]))
            {
                Directory.CreateDirectory(_parsedArguments[ResultsFileArgumentPath]);
            }

            _resultsPath = Path.Combine(_parsedArguments[ResultsFileArgumentPath], _parsedArguments[ResultsFileArgumentName]);
            _excludeCategoriesDir = _parsedArguments.GetValueOrDefault(ExcludeCategoriesDirArgumentName);
            _excludeCategoriesFile = _parsedArguments.GetValueOrDefault(ExcludeCategoriesFileArgumentName);
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
                Tuple.Create<string, Action<string, bool>, bool>(_parsedArguments.GetValueOrDefault(ExcludeMethodArgumentName), testRunner.SkipMethod, true),
                Tuple.Create<string, Action<string, bool>, bool>(_parsedArguments.GetValueOrDefault(ExcludeClassArgumentName), testRunner.SkipClass, true),
                Tuple.Create<string, Action<string, bool>, bool>(_parsedArguments.GetValueOrDefault(IncludeMethodArgumentName), testRunner.SkipMethod, false),
                Tuple.Create<string, Action<string, bool>, bool>(_parsedArguments.GetValueOrDefault(IncludeClassArgumentName), testRunner.SkipClass, false)
            };

            foreach (var t in filters)
            {
                ConfigureFilters(t.Item1, t.Item2, t.Item3);
            }

            return testRunner;
        }
    }
}
