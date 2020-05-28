// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
using Xunit.ConsoleClient;

namespace Microsoft.DotNet.XHarness.WebAssembly
{
    class MsgBus : IMessageBus
    {
        public MsgBus(IMessageSink messageSink)
        {
            Sink = messageSink;
        }

        IMessageSink Sink { get; set; }

        public bool QueueMessage(IMessageSinkMessage message)
        {
            return Sink.OnMessage(message);
        }

        public void Dispose()
        {
        }
    }

    class Discoverer : XunitTestFrameworkDiscoverer
    {
        private readonly List<ITestClass> _testClasses;
        private readonly IMessageSink _sink;
        private readonly ITestFrameworkDiscoveryOptions _discoveryOptions;
        private readonly IXunitTestCollectionFactory _testCollectionFactory;
        int _nenumerated;

        public Discoverer(IAssemblyInfo assemblyInfo,
                          ISourceInformationProvider sourceProvider,
                          IMessageSink diagnosticMessageSink,
                          ITestFrameworkDiscoveryOptions discoveryOptions,
                          IXunitTestCollectionFactory collectionFactory = null)
            : base(assemblyInfo, sourceProvider, diagnosticMessageSink, collectionFactory)
        {
            _testCollectionFactory = collectionFactory;
            _sink = diagnosticMessageSink;
            _discoveryOptions = discoveryOptions ?? throw new ArgumentNullException(nameof(discoveryOptions));

            _testClasses = new List<ITestClass>();
            foreach (ITypeInfo type in AssemblyInfo.GetTypes(false).Where(IsValidTestClass))
                _testClasses.Add(CreateTestClass(type));
        }

        protected override ITestClass CreateTestClass(ITypeInfo @class)
        {
            return new TestClass(_testCollectionFactory.Get(@class), @class);
        }

        public void Discover()
        {
            Console.WriteLine("Discovering tests...");
            using (var messageBus = new MsgBus(_sink))
            {
                foreach (ITestClass testClass in _testClasses)
                {
                    if (testClass.Class.Name == "System.Threading.ThreadPools.Tests.ThreadPoolTests")
                        // FIXME: This invokes the static ctor which creates threads
                        continue;
                    try
                    {
                        FindTestsForType(testClass, false, messageBus, _discoveryOptions);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("EX: " + ex);
                    }

                    _nenumerated++;
                    if (_nenumerated % 10 == 0)
                        Console.WriteLine($" { _nenumerated * 100 / _testClasses.Count } %");
                }
            }
        }
    }

    public class WasmRunner : IMessageSink
    {
        private readonly ITestFrameworkDiscoveryOptions _discoveryOptions;
        private readonly Discoverer _discoverer;
        private List<ITestCase> _testCases;
        private readonly XunitProject _project;
        private string _last_name = string.Empty;
        public int nrun, nfail, nskipped, nfiltered;

        IEnumerable<ITestCase> TestCases {
            get {
                if (_testCases == null)
                {
                    _testCases = new List<ITestCase>();
                    _discoverer.Discover();
                    Console.WriteLine($"Discovered tests: {_testCases.Count }");
                }

                foreach (ITestCase testCase in _testCases)
                {
                    yield return testCase;
                }
            }
        }

        public WasmRunner(XunitProject project)
        {
            _project = project;

            string assemblyFileName = "/" + project.Assemblies.FirstOrDefault().AssemblyFilename;

            var assembly = Assembly.LoadFrom(assemblyFileName);
            var assemblyInfo = new ReflectionAssemblyInfo(assembly);

            IAttributeInfo collectionBehaviorAttribute = assemblyInfo.GetCustomAttributes(typeof(CollectionBehaviorAttribute)).SingleOrDefault();
            var testAssembly = new TestAssembly(assemblyInfo, null);

            IXunitTestCollectionFactory collectionFactory = ExtensibilityPointFactory.GetXunitTestCollectionFactory(this, collectionBehaviorAttribute, testAssembly);

            _discoveryOptions = TestFrameworkOptions.ForDiscovery(null);
            _discoverer = new Discoverer(assemblyInfo, new NullSourceInformationProvider(), this, _discoveryOptions, collectionFactory);
        }

        static object ConvertArg(object arg, Type argType)
        {
            if (arg == null || arg.GetType() == argType)
                return arg;
            // Not clear what conversions should be done
            // IEnumerable<T> -> T[]
            if (argType.IsArray)
            {
                Type t = typeof(IEnumerable<>).MakeGenericType(argType.GetElementType());
                if (t.IsAssignableFrom(arg.GetType()))
                {
                    MethodInfo m = typeof(Enumerable).GetMethod("ToArray").MakeGenericMethod(new Type[] { argType.GetElementType() });
                    return m.Invoke(null, new object[] { arg });
                }
            }
            try
            {
                if (arg != null && arg is IConvertible)
                    arg = Convert.ChangeType(arg, argType);
            }
            catch (Exception ex)
            {
                Console.WriteLine("EX (ConvertArg): " + ex);
            }

            return arg;
        }

        public virtual bool OnMessage(IMessageSinkMessage msg)
        {
            if (msg is Xunit.Sdk.TestCaseDiscoveryMessage disc_msg)
                _testCases.Add(disc_msg.TestCase);

            return true;
        }

        public int Run()
        {
            Console.WriteLine("Running tests...");
            Console.WriteLine(".");

            foreach (ITestCase testCase in TestCases)
            {
                RunTestCase(testCase);
            }

            Console.WriteLine($"TESTS = { TestCases.Count() }, RUN = { nrun }, SKIP = { nfiltered }, FAIL = { nfail }");
            return nfail == 0 ? 0 : 1;
        }

        private void RunTestCase(ITestCase testCase)
        {
            var tc = testCase as XunitTestCase;

            if (!_project.Filters.Filter(tc))
            {
                nfiltered++;
                return;
            }

            // var itrait_attrs = tc.TestMethod.Method.GetCustomAttributes(typeof(ITraitAttribute));
            // // FIXME:
            // if (itrait_attrs.Count () > 0) {
            // 	Console.WriteLine ("SKIP (ITraitAttribute): " + tc.DisplayName);
            // 	nfiltered ++;
            // 	continue;
            // }
            /*
            foreach (var attr in itrait_attrs) {
                Console.WriteLine (attr);
                foreach (var disc_attr in attr.GetCustomAttributes (typeof (TraitDiscovererAttribute))) {
                    Console.WriteLine (disc_attr);

                }
            }
            */

            MethodInfo method = (tc.Method as ReflectionMethodInfo)?.MethodInfo;

            if (method.ReflectedType.IsGenericTypeDefinition)
            {
                Console.WriteLine("FAIL (generic): " + tc.DisplayName);
                nfail++;
                return;
            }

            if (tc is XunitTheoryTestCase)
            {
                RunTheory(tc, method);
            }
            else
            {
                RunFact(tc, method);
            }
        }

        private void RunTheory(XunitTestCase tc, MethodInfo method)
        {
            if (method.IsGenericMethod)
            {
                Console.WriteLine("SKIP (generic): " + tc.DisplayName);
                nfiltered++;
                return;
            }

            // From XunitTheoryTestCaseRunner
            IEnumerable<IAttributeInfo> attrs = tc.TestMethod.Method.GetCustomAttributes(typeof(DataAttribute));
            bool failed = false;
            foreach (IAttributeInfo dataAttribute in attrs)
            {
                IAttributeInfo discovererAttribute = dataAttribute.GetCustomAttributes(typeof(DataDiscovererAttribute)).First();
                var args = discovererAttribute.GetConstructorArguments().Cast<string>().ToList();

                Type discovererType = null;
                if (args[1] == "xunit.core")
                    discovererType = typeof(IXunitTestCollectionFactory).Assembly.GetType(args[0]);
                if (discovererType == null)
                {
                    Console.WriteLine("FAIL (discoverer): " + args[0] + " " + args[1]);
                    failed = true;
                }

                IDataDiscoverer discoverer;
                discoverer = ExtensibilityPointFactory.GetDataDiscoverer(this, discovererType);

                try
                {
                    IEnumerable<object[]> data = discoverer.GetData(dataAttribute, tc.TestMethod.Method);
                    object[][] data_arr = data.ToArray();
                    Console.WriteLine(tc.DisplayName + " [" + data_arr.Length + "]");
                    foreach (object[] dataRow in data_arr)
                    {
                        nrun++;
                        object obj = GetObject(method);
                        ParameterInfo[] pars = method.GetParameters();
                        for (int i = 0; i < dataRow.Length; ++i)
                            dataRow[i] = ConvertArg(dataRow[i], pars[i].ParameterType);
                        method.Invoke(obj, BindingFlags.Default, null, dataRow, null);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("FAIL: " + tc.DisplayName + " " + ex);
                    failed = true;
                }
            }
            if (failed)
            {
                nfail++;
                return;
            }
        }

        private void RunFact(XunitTestCase tc, MethodInfo method)
        {
            if (!string.IsNullOrEmpty(tc.SkipReason))
            {
                nskipped++;
                return;
            }
            nrun++;
            string name = tc.DisplayName;
            int indx = name.IndexOf("(");
            if (indx != -1)
                name = name.Substring(0, indx);
            if (name != _last_name)
                Console.WriteLine(name);
            _last_name = name;
            try
            {
                object obj = GetObject(method);
                object[] args = tc.TestMethodArguments;
                if (args != null)
                {
                    ParameterInfo[] pars = method.GetParameters();
                    for (int i = 0; i < args.Length; ++i)
                    {
                        args[i] = ConvertArg(args[i], pars[i].ParameterType);
                    }
                }
                method.Invoke(obj, args);
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL: " + tc.DisplayName);
                Console.WriteLine(ex);
                nfail++;
            }
        }

        private object GetObject(MethodInfo method)
        {
            if (!method.IsStatic)
            {
                ConstructorInfo constructor = method.ReflectedType.GetConstructor(Type.EmptyTypes);
                if (constructor != null)
                    return constructor.Invoke(null);
                else
                    return System.Runtime.Serialization.FormatterServices.GetUninitializedObject(method.ReflectedType);
            }

            return null;
        }
    }

    public class CmdLineParser : CommandLine
    {
        public CmdLineParser(string[] args) : base(args, s => true)
        {
        }
    }
}
