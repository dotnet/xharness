// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.Common.Resources;
using Xunit;

namespace Microsoft.DotNet.XHarness.CLI.Tests.Resources;

public class StringsTests
{
    [Fact]
    public void ResourcesCanBeLoaded()
    {
        // Test that we can load resources from the assembly
        Assert.NotNull(Strings.Apple_Test_Description);
        Assert.NotEmpty(Strings.Apple_Test_Description);
        Assert.Contains("TestRunner", Strings.Apple_Test_Description);
    }

    [Fact]
    public void CommonErrorMessagesExist()
    {
        // Test that common error message templates exist
        Assert.NotNull(Strings.Error_UnknownArguments);
        Assert.Contains("{0}", Strings.Error_UnknownArguments);
        
        Assert.NotNull(Strings.Error_RequiredArgumentMissing);
        Assert.Contains("{0}", Strings.Error_RequiredArgumentMissing);
        
        Assert.NotNull(Strings.Error_InvalidValue);
        Assert.Contains("{0}", Strings.Error_InvalidValue);
    }

    [Fact]
    public void HelpMessagesExist()
    {
        // Test that help message templates exist
        Assert.NotNull(Strings.Help_Usage);
        Assert.Contains("{0}", Strings.Help_Usage);
        
        Assert.NotNull(Strings.Help_CommandNotAvailableOnNonOSX);
        Assert.Contains("{0}", Strings.Help_CommandNotAvailableOnNonOSX);
    }

    [Fact]
    public void ArgumentDescriptionsExist()
    {
        // Test that argument descriptions exist
        Assert.NotNull(Strings.Arg_Target_Description);
        Assert.NotEmpty(Strings.Arg_Target_Description);
        
        Assert.NotNull(Strings.Arg_AppPath_Description);
        Assert.NotEmpty(Strings.Arg_AppPath_Description);
        
        Assert.NotNull(Strings.Arg_Help_Description);
        Assert.NotEmpty(Strings.Arg_Help_Description);
    }

    [Fact]
    public void LogMessagesExist()
    {
        // Test that log message templates exist
        Assert.NotNull(Strings.Log_XHarnessCommandIssued);
        Assert.Contains("{0}", Strings.Log_XHarnessCommandIssued);
        Assert.Contains("{1}", Strings.Log_XHarnessCommandIssued);
        
        Assert.NotNull(Strings.Log_XHarnessExitCode);
        Assert.Contains("{0}", Strings.Log_XHarnessExitCode);
    }
}