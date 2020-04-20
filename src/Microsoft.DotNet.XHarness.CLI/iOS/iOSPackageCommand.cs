// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI.Common;
using Microsoft.Extensions.Logging;
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
    internal class iOSPackageCommand : PackageCommand
    {
        private readonly iOSPackageCommandArguments _arguments = new iOSPackageCommandArguments();

        protected override IPackageCommandArguments PackageArguments => _arguments;

        public iOSPackageCommand() : base()
        {
            Options = new OptionSet() {
                "usage: ios package [OPTIONS]",
                "",
                "Packaging command that will create a iOS/tvOS/watchOS or macOS application that can be used to run NUnit or XUnit-based test dlls",
                { "mtouch-extraargs=|m=", "Extra arguments to be passed to mtouch.", v => _arguments.MtouchExtraArgs = v },
                { "ignore-directory=|i=", "Root directory containing all the *.ignore files used to skip tests if needed.", v => _arguments.IgnoreFilesRootDirectory = v },
                { "template=|t=", "Indicates which template to use. There are two available ones: Managed, which uses Xamarin.[iOS|Mac] and Native (default:Managed).",
                    v=> {
                        if (Enum.TryParse<TemplateType>(v, out TemplateType template)) {
                            _arguments.SelectedTemplateType = template;
                        } else
                        {
                            _log.LogInformation($"Unknown template type '{v}'");
                            ShowHelp = true;
                        }
                    }
                },
                { "traits-directory=|td=", "Root directory that contains all the .txt files with traits that will be skipped if needed.", v =>  _arguments.TraitsRootDirectory = v },
            };

            foreach (var option in CommonOptions)
            {
                Options.Add(option);
            }
        }

        protected override Task<ExitCode> InvokeInternal()
        {
            _log.LogDebug($"iOS Package command called:{Environment.NewLine}Application Name = {_arguments.AppPackageName}");
            _log.LogDebug($"Working Directory:{_arguments.WorkingDirectory}{Environment.NewLine}Output Directory:{_arguments.OutputDirectory}");
            _log.LogDebug($"Ignore Files Root Directory:{_arguments.IgnoreFilesRootDirectory}{Environment.NewLine}Traits Root Directory:{_arguments.TraitsRootDirectory}");
            _log.LogDebug($"MTouch Args:{_arguments.MtouchExtraArgs}{Environment.NewLine}Template Type:{Enum.GetName(typeof(TemplateType), _arguments.SelectedTemplateType)}");

            return Task.FromResult(ExitCode.SUCCESS);
        }
    }
}
