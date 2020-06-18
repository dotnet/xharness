// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.DotNet.XHarness.iOS.Shared.TestImporter;

namespace Microsoft.DotNet.XHarness.iOS.TestImporter
{
    /// <summary>
    /// Default implemenation of the assmebly locator, because we are working
    /// with a command line, all the assemblies will be considered to be in 
    /// the current directory, keeps things simple unless we realize that a more
    /// complicated locator is needed.
    /// </summary>
    public class AssemblyLocator : IAssemblyLocator
    {
        private readonly string _assembliesRootPath;

        public AssemblyLocator(string assembliesRootPath)
        {
            if (string.IsNullOrEmpty(assembliesRootPath))
            {
                _assembliesRootPath = Directory.GetCurrentDirectory();
            }
            else
            {
                _assembliesRootPath = assembliesRootPath;
            }
            // validate that we can indeed find the dir
            if (!Directory.Exists(_assembliesRootPath))
            {
                throw new ArgumentException($"Dir {_assembliesRootPath} could not be found.");
            }
        }

        public string GetAssembliesLocation(Platform _) => _assembliesRootPath;

        public string GetAssembliesRootLocation(Platform _) => _assembliesRootPath;

        public string GetHintPathForReferenceAssembly(string assembly, Platform _) => Path.Combine(_assembliesRootPath, assembly);

        public string GetTestingFrameworkDllPath(string assembly, Platform platform) => Path.Combine(_assembliesRootPath, assembly);
    }
}
