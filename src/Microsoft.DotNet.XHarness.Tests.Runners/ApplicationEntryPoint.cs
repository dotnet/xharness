// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Tests.Runners.Core;

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
    /// Subclasses must provide the minimun implementation to ensure that:
    ///
    /// Device: We do have the required device information for the logger.
    /// Assemblies: Provide a list of the assembly information to run.
    ///     assemblies can be loaded from disk or from memory, is up to the 
    ///     implementor.
    /// </summary>
    public abstract class ApplicationEntryPoint
    {

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
    }
}
