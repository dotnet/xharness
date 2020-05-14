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
        private readonly IEnumerator<ITestClass> _testClassesEnumerator;
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
            _testClassesEnumerator = _testClasses.GetEnumerator();
        }

        protected override ITestClass CreateTestClass(ITypeInfo @class)
        {
            return new TestClass(_testCollectionFactory.Get(@class), @class);
        }

        public bool Step()
        {
            if (!_testClassesEnumerator.MoveNext())
                return false;

            using (var messageBus = new MsgBus(_sink))
            {
                ITestClass testClass = _testClassesEnumerator.Current;
                //Console.WriteLine (testClass.Class.Name);
                if (testClass.Class.Name == "System.Threading.ThreadPools.Tests.ThreadPoolTests")
                    // FIXME: This invokes the static ctor which creates threads
                    return true;
                try
                {
                    FindTestsForType(testClass, false, messageBus, _discoveryOptions);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("EX: " + ex);
                }
            }

            _nenumerated++;
            if (_nenumerated % 10 == 0)
                Console.WriteLine("" + (_nenumerated * 100) / _testClasses.Count + "%");

            return true;
        }
    }

    class WasmRunner : IMessageSink
    {
        private readonly ITestFrameworkDiscoveryOptions _discoveryOptions;
        private readonly Discoverer _discoverer;
        private readonly ITestFrameworkExecutor _executor;
        private readonly ITestFrameworkExecutionOptions _executionOptions;
        private readonly List<ITestCase> _testCases;
        private readonly XunitProject _project;

        public WasmRunner(XunitProject project)
        {
            _project = project;

            string assemblyFileName = "/" + project.Assemblies.First().AssemblyFilename;
            _testCases = new List<ITestCase>();

            var assembly = Assembly.LoadFrom(assemblyFileName);
            var assemblyInfo = new Xunit.Sdk.ReflectionAssemblyInfo(assembly);

            IAttributeInfo collectionBehaviorAttribute = assemblyInfo.GetCustomAttributes(typeof(CollectionBehaviorAttribute)).SingleOrDefault();
            var testAssembly = new TestAssembly(assemblyInfo, null);

            IXunitTestCollectionFactory collectionFactory = ExtensibilityPointFactory.GetXunitTestCollectionFactory(this, collectionBehaviorAttribute, testAssembly);

            /*
            object res = null;
            res = Activator.CreateInstance (typeof (Xunit.Sdk.MemberDataDiscoverer), true, true);
            Console.WriteLine ("DISC2: " + res);
            */

            _discoveryOptions = TestFrameworkOptions.ForDiscovery(null);
            _executionOptions = TestFrameworkOptions.ForExecution(null);

            _discoverer = new Discoverer(assemblyInfo, new NullSourceInformationProvider(), this, _discoveryOptions, collectionFactory);

            _executor = new XunitTestFrameworkExecutor(assembly.GetName(), new NullSourceInformationProvider(), this);
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
            {
                _testCases.Add(disc_msg.TestCase);
                return true;
            }
            //Console.WriteLine ("MSG:" + msg);		
            /*
            if (msg is Xunit.Sdk.DiagnosticMessage dmsg)
                Console.WriteLine ("MSG:" + dmsg.Message);
            else if (msg is Xunit.Sdk.TestCaseDiscoveryMessage disc_msg)
                Console.WriteLine ("TEST:" + disc_msg.TestCase.DisplayName);
            else
                Console.WriteLine ("MSG:" + msg);
            */
            return true;
        }

        public int nrun, nfail, nskipped, nfiltered;

        public int Run()
        {
            int tc_index;

            Console.WriteLine("Discovering tests...");

            int n = 0;
            while (n < 20)
            {
                if (!_discoverer.Step())
                {
                    break;
                }
            }

            Console.WriteLine("Running " + _testCases.Count + " tests...");
            tc_index = 0;

            Console.WriteLine(".");
            string last_name = "";
            while (true)
            {
                if (tc_index == _testCases.Count)
                    break;
                var tc = _testCases[tc_index] as XunitTestCase;

                tc_index++;

                if (!_project.Filters.Filter(tc))
                {
                    nfiltered++;
                    continue;
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
                    continue;
                }

                if (tc is Xunit.Sdk.XunitTheoryTestCase)
                {
                    if (method.IsGenericMethod)
                    {
                        Console.WriteLine("SKIP (generic): " + tc.DisplayName);
                        nfiltered++;
                        continue;
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
                                object obj = null;
                                if (!method.IsStatic)
                                {
                                    ConstructorInfo constructor = method.ReflectedType.GetConstructor(Type.EmptyTypes);
                                    if (constructor != null)
                                        obj = constructor.Invoke(null);
                                    else
                                        obj = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(method.ReflectedType);
                                }

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
                        continue;
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(tc.SkipReason))
                    {
                        nskipped++;
                        continue;
                    }
                    nrun++;
                    string name = tc.DisplayName;
                    int indx = name.IndexOf("(");
                    if (indx != -1)
                        name = name.Substring(0, indx);
                    if (name != last_name)
                        Console.WriteLine(name);
                    last_name = name;
                    //Console.WriteLine (tc.DisplayName);
                    try
                    {
                        object obj = null;
                        if (!method.IsStatic)
                        {
                            ConstructorInfo constructor = method.ReflectedType.GetConstructor(Type.EmptyTypes);
                            if (constructor != null)
                                obj = constructor.Invoke(null);
                            else
                                obj = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(method.ReflectedType);
                        }
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
            }

            //foreach (var tc in testCases)
            //	Console.WriteLine (tc.DisplayName);
            //executor.RunTests (testCases, this, executionOptions);
            Console.WriteLine("TESTS = " + _testCases.Count + ", RUN = " + nrun + ", SKIP = " + nfiltered + ", FAIL = " + nfail);
            return nfail == 0 ? 0 : 1;
        }
    }

    class CmdLineParser : CommandLine
    {
        public CmdLineParser(string[] args) : base(args, s => true)
        {
        }
    }

    public class XunitDriver
    {
        static WasmRunner s_testRunner;

        static int Main(string[] args)
        {
            // Process rsp files
            // FIXME: This doesn't work with wasm
            /*
            var new_args = new List<string> ();
            foreach (var arg in args) {
                if (arg [0] == '@') {
                    foreach (var line in File.ReadAllLines ("/" + arg.Substring (1)))
                        new_args.Add (line);
                } else {
                    new_args.Add (arg);
                }
            }
            */
            var cmdline = new CmdLineParser(args);
            s_testRunner = new WasmRunner(cmdline.Project);
            return s_testRunner.Run();
        }
    }
}
