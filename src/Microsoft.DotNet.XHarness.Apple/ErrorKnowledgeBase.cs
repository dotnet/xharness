// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;

namespace Microsoft.DotNet.XHarness.Apple;

public class ErrorKnowledgeBase : IErrorKnowledgeBase
{
    private static readonly Dictionary<string, KnownIssue> s_testErrorMaps = new()
    {
        ["Failed to communicate with the device"] =
            new(Microsoft.DotNet.XHarness.Common.Resources.Strings.Apple_ErrorKnowledgeBase_FailedCommunicateDevice,
                suggestedExitCode: (int)ExitCode.DEVICE_FAILURE),

        ["MT1031"] =
            new(Microsoft.DotNet.XHarness.Common.Resources.Strings.Apple_ErrorKnowledgeBase_DeviceLockedMT1031,
                suggestedExitCode: (int)ExitCode.DEVICE_FAILURE),

        ["the device is locked"] =
            new(Microsoft.DotNet.XHarness.Common.Resources.Strings.Apple_ErrorKnowledgeBase_DeviceLocked,
                suggestedExitCode: (int)ExitCode.DEVICE_FAILURE),

        ["while Setup Assistant is running"] =
            new(Microsoft.DotNet.XHarness.Common.Resources.Strings.Apple_ErrorKnowledgeBase_SetupAssistantRunning,
                suggestedExitCode: (int)ExitCode.DEVICE_FAILURE),

        ["LSOpenURLsWithRole() failed with error -10825"] =
            new(Microsoft.DotNet.XHarness.Common.Resources.Strings.Apple_ErrorKnowledgeBase_RequiresNewerMacOS,
                suggestedExitCode: (int)ExitCode.GENERAL_FAILURE),

        ["Failed to start launchd_sim: could not bind to session"] =
            new(Microsoft.DotNet.XHarness.Common.Resources.Strings.Apple_ErrorKnowledgeBase_LaunchdSimBindError,
                suggestedExitCode: (int)ExitCode.APP_LAUNCH_FAILURE),

        ["error HE0018: Could not launch the simulator application"] =
            new(Microsoft.DotNet.XHarness.Common.Resources.Strings.Apple_ErrorKnowledgeBase_FailedLaunchSimulator,
                suggestedExitCode: (int)ExitCode.SIMULATOR_FAILURE),

        ["error HE0042: Could not launch the app"] =
            new(Microsoft.DotNet.XHarness.Common.Resources.Strings.Apple_ErrorKnowledgeBase_FailedLaunchApp,
                suggestedExitCode: (int)ExitCode.APP_LAUNCH_FAILURE),
       
        ["[TCP tunnel] Xamarin.Hosting: Failed to connect to port"] = new(
            Microsoft.DotNet.XHarness.Common.Resources.Strings.Apple_ErrorKnowledgeBase_TcpTunnelConnectionFailed,
            suggestedExitCode: (int)ExitCode.TCP_CONNECTION_FAILED),
    };

    private static readonly Dictionary<string, KnownIssue> s_buildErrorMaps = new();

    private static readonly Dictionary<string, KnownIssue> s_installErrorMaps = new()
    {
        ["IncorrectArchitecture"] =
            new(Microsoft.DotNet.XHarness.Common.Resources.Strings.Apple_ErrorKnowledgeBase_IncorrectArchitecture), // known failure, but not an issue

        ["0xe8008015"] =
            new(Microsoft.DotNet.XHarness.Common.Resources.Strings.Apple_ErrorKnowledgeBase_NoValidProvisioningProfile, suggestedExitCode: (int)ExitCode.APP_NOT_SIGNED),

        ["valid provisioning profile for this executable was not found"] =
            new(Microsoft.DotNet.XHarness.Common.Resources.Strings.Apple_ErrorKnowledgeBase_NoValidProvisioningProfile, suggestedExitCode: (int)ExitCode.APP_NOT_SIGNED),

        ["0xe800801c"] =
            new(Microsoft.DotNet.XHarness.Common.Resources.Strings.Apple_ErrorKnowledgeBase_AppNotSigned, suggestedExitCode: (int)ExitCode.APP_NOT_SIGNED),

        ["No code signature found"] =
            new(Microsoft.DotNet.XHarness.Common.Resources.Strings.Apple_ErrorKnowledgeBase_AppNotSigned, suggestedExitCode: (int)ExitCode.APP_NOT_SIGNED),
    };

    public bool IsKnownBuildIssue(IFileBackedLog buildLog, [NotNullWhen(true)] out KnownIssue? knownFailureMessage)
        => TryFindErrors(buildLog, s_buildErrorMaps, out knownFailureMessage);

    public bool IsKnownTestIssue(IFileBackedLog runLog, [NotNullWhen(true)] out KnownIssue? knownFailureMessage)
        => TryFindErrors(runLog, s_testErrorMaps, out knownFailureMessage);

    public bool IsKnownInstallIssue(IFileBackedLog installLog, [NotNullWhen(true)] out KnownIssue? knownFailureMessage)
        => TryFindErrors(installLog, s_installErrorMaps, out knownFailureMessage);

    private static bool TryFindErrors(IFileBackedLog log, Dictionary<string, KnownIssue> errorMap,
        [NotNullWhen(true)] out KnownIssue? failureMessage)
    {
        failureMessage = null;
        if (log == null)
        {
            return false;
        }

        if (!File.Exists(log.FullPath) || new FileInfo(log.FullPath).Length <= 0)
        {
            return false;
        }

        using var reader = log.GetReader();
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (line == null)
            {
                continue;
            }

            //go over errors and return true as soon as we find one that matches
            foreach (var error in errorMap.Keys)
            {
                if (!line.Contains(error, StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                failureMessage = errorMap[error];
                return true;
            }
        }

        return false;
    }
}
