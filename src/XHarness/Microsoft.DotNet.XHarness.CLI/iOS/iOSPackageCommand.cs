// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Mono.Options;
using System;
using System.Collections.Generic;

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
        string WorkingDirectory;
        // will be used as the output dir of the generated projects.
        string OutputDirectory;
        // path that is the root of the .ignore files that will be used to skip tests if needed.
        string IgnoreFilesRootDirectory;
        // path that is the root of the traits txt files that will be used to skip tests if needed.
        string TraitsRootDirectory;

        string ApplicationName;
        string MtouchExtraArgs;
        TemplateType SelectedTemplateType;
        bool ShowHelp = false;

        public iOSPackageCommand() : base("package")
        {
            Options = new OptionSet() {
                "usage: ios package [OPTIONS]",
                "",
                "Packaging command that will create a iOS/tvOS/watchOS or macOS application that can be used to run NUnit or XUnit-based test dlls",
                { "mtouch-extraargs=|m=", "Extra arguments to be passed to mtouch.", v => MtouchExtraArgs = v },
                { "ignore-directory=|i=", "Root directory containing all the *.ignore files used to skip tests if needed.", v => IgnoreFilesRootDirectory = v },
                { "name=|n=", "Name of the test application",  v => ApplicationName = v},
                { "output-directory=|o=", "Directory in which the resulting package will be outputted", v => OutputDirectory = v},
                { "template=|t=", "Indicates which template to use. There are two available ones: Managed, which uses Xamarin.[iOS|Mac] and Native (default:Managed).",
                    v=> {
                        if (Enum.TryParse<TemplateType>(v, out var template)) {
                            SelectedTemplateType = template;
                        } else
                        {
                            Console.WriteLine($"Unknown template type '{v}'");
                            ShowHelp = true;
                        }
                    }
                },
                { "traits-directory=|td=", "Root directory that contains all the .txt files with traits that will be skipped if needed.", v =>  TraitsRootDirectory = v },
                { "working-directory=|w=", "Directory that will be used to output generated projects", v => WorkingDirectory = v },
                { "help|h", "Show this message", v => ShowHelp = v != null },
            };
        }

        public override int Invoke(IEnumerable<string> arguments)
        {
            // Deal with unknown options and print nicely
            var extra = Options.Parse(arguments);
            if (ShowHelp)
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
            Console.WriteLine($"iOS Package command called:{Environment.NewLine}Application Name = {ApplicationName}");
            Console.WriteLine($"Working Directory:{WorkingDirectory}{Environment.NewLine}Output Directory:{OutputDirectory}");
            Console.WriteLine($"Ignore Files Root Directory:{IgnoreFilesRootDirectory}{Environment.NewLine}Traits Root Directory:{TraitsRootDirectory}");
            Console.WriteLine($"MTouch Args:{MtouchExtraArgs}{Environment.NewLine}Template Type:{Enum.GetName(typeof(TemplateType), SelectedTemplateType)}");

            return 0;
        }
    }
}
