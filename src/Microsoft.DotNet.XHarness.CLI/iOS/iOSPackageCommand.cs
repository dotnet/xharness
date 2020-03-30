// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.iOS
{

    // Represents the template to be used. Currently only supports the Managed one.
    public enum TemplateType
    {
        Managed,
        Native,
    }

    // Command that will create the required project generation for the iOS plaform. The command will ensure that all
    // required .csproj and src are created. The command is part of the parent CommandSet iOS and exposes similar
    // plus extra options to the one that its Android counterpart exposes.
    public class iOSPackageCommand : Command
    {
        // working directories
        private string _workingDirectory;

        // will be used as the output dir of the generated projects.
        private string _outputDirectory;

        // path that is the root of the .ignore files that will be used to skip tests if needed.
        private string _ignoreFilesRootDirectory;

        // path that is the root of the traits txt files that will be used to skip tests if needed.
        private string _traitsRootDirectory;
        private string _applicationName;
        private string _mtouchExtraArgs;
        private TemplateType _selectedTemplateType;
        private bool _showHelp = false;

        public iOSPackageCommand() : base("package")
        {
            Options = new OptionSet() {
                "usage: ios package [OPTIONS]",
                "",
                "Packaging command that will create a iOS/tvOS/watchOS or macOS application that can be used to run NUnit or XUnit-based test dlls",
                { "mtouch-extraargs=|m=", "Extra arguments to be passed to mtouch.", v => _mtouchExtraArgs = v },
                { "ignore-directory=|i=", "Root directory containing all the *.ignore files used to skip tests if needed.", v => _ignoreFilesRootDirectory = v },
                { "name=|n=", "Name of the test application",  v => _applicationName = v},
                { "output-directory=|o=", "Directory in which the resulting package will be outputted", v => _outputDirectory = v},
                { "template=|t=", "Indicates which template to use. There are two available ones: Managed, which uses Xamarin.[iOS|Mac] and Native (default:Managed).",
                    v=> {
                        if (Enum.TryParse<TemplateType>(v, out TemplateType template)) {
                            _selectedTemplateType = template;
                        } else
                        {
                            Console.WriteLine($"Unknown template type '{v}'");
                            _showHelp = true;
                        }
                    }
                },
                { "traits-directory=|td=", "Root directory that contains all the .txt files with traits that will be skipped if needed.", v =>  _traitsRootDirectory = v },
                { "working-directory=|w=", "Directory that will be used to output generated projects", v => _workingDirectory = v },
                { "help|h", "Show this message", v => _showHelp = v != null },
            };
        }

        public override int Invoke(IEnumerable<string> arguments)
        {
            // Deal with unknown options and print nicely
            List<string> extra = Options.Parse(arguments);
            if (_showHelp)
            {
                Options.WriteOptionDescriptions(Console.Out);
                return 1;
            }
            if (extra.Count > 0)
            {
                Console.WriteLine($"Unknown arguments{string.Join(" ", extra)}");
                Options.WriteOptionDescriptions(Console.Out);
                return 2;
            }
            Console.WriteLine($"iOS Package command called:{Environment.NewLine}Application Name = {_applicationName}");
            Console.WriteLine($"Working Directory:{_workingDirectory}{Environment.NewLine}Output Directory:{_outputDirectory}");
            Console.WriteLine($"Ignore Files Root Directory:{_ignoreFilesRootDirectory}{Environment.NewLine}Traits Root Directory:{_traitsRootDirectory}");
            Console.WriteLine($"MTouch Args:{_mtouchExtraArgs}{Environment.NewLine}Template Type:{Enum.GetName(typeof(TemplateType), _selectedTemplateType)}");

            return 0;
        }
    }
}
