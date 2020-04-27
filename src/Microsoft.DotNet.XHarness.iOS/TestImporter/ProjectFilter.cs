using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.XHarness.iOS.Shared.TestImporter;
using Microsoft.DotNet.XHarness.iOS.Shared.TestImporter.Templates;

namespace Microsoft.DotNet.XHarness.iOS.TestImporter
{
    /// <summary>
    /// Default project filter implementation of the command line. Since we are
    /// using a command line that specifies the assmemblies and the 
    /// </summary>
    public class ProjectFilter : IProjectFilter
    {
        private readonly string _ignoredFilesRootPath;
        private readonly string _traitFilesRootPath;

        public ProjectFilter(string ignoredFilesRootPath, string traitFilesRootPath)
        {
            if (string.IsNullOrEmpty(ignoredFilesRootPath))
                // use the current directory as the source of the ignore files
                _ignoredFilesRootPath = Directory.GetCurrentDirectory ();
            else 
                _ignoredFilesRootPath = ignoredFilesRootPath;
            if (string.IsNullOrEmpty(traitFilesRootPath))
                // same as with the ignore files, do use the current dir
                _traitFilesRootPath = Directory.GetCurrentDirectory ();
            else
                _traitFilesRootPath = traitFilesRootPath;
            // validate that the dirs do exist
            if (!Directory.Exists(_ignoredFilesRootPath))
                throw new ArgumentException($"Dir {_ignoredFilesRootPath} could not be found.");
            if (!Directory.Exists(_traitFilesRootPath))
                throw new ArgumentException($"Dir {_traitFilesRootPath} could not be found.");
        }

        // never exclude dlls that are passed from the cmd
        public bool ExcludeDll(Platform platform, string assembly) => false;

        // never exclude projects that are passed from the cmd
        public bool ExludeProject(ProjectDefinition project, Platform platform) => false;

        // return the list of files that to map the pattern, user should have place all of them there
        public IEnumerable<string> GetIgnoreFiles(string projectName, List<(string assembly, string hintPath)> assemblies, Platform _) =>
            Directory.GetFiles(_ignoredFilesRootPath, "*.ignore", SearchOption.AllDirectories);

        public IEnumerable<string> GetTraitsFiles(Platform _)
        {
            // because we are working from the command line, it is expected that
            // the traits are comming from the same dir and we can ignore the 
            // platform the user calling the tool should have place the correct
            // trait files. If he added wrong trait files, is for the user to
            // blame
            foreach (var file in new[] { "nunit-excludes.txt", "xunit-excludes.txt" }) {
                var traitFile = Path.Combine(_traitFilesRootPath, file);
                if (File.Exists(traitFile))
                    yield return traitFile;
            }
        }
    }
}
