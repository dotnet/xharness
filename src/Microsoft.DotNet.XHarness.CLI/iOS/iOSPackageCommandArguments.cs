﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.DotNet.XHarness.CLI.Common;

namespace Microsoft.DotNet.XHarness.CLI.iOS
{
    internal class iOSPackageCommandArguments : PackageCommandArguments
    {
        /// <summary>
        /// A path that is the root of the .ignore files that will be used to skip tests if needed
        /// </summary>
        public string IgnoreFilesRootDirectory { get; set; }

        /// <summary>
        /// A path that is the root of the traits txt files that will be used to skip tests if needed
        /// </summary>
        public string TraitsRootDirectory { get; set; }

        public string MtouchExtraArgs { get; set; }

        public TemplateType SelectedTemplateType { get; set; }

        public override IList<string> GetValidationErrors()
        {
            IList<string> errors = base.GetValidationErrors();

            // TODO: Validate arguments above
            return errors;
        }
    }
}
