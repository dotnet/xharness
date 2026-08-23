// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.XHarness.Android.Execution;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.Android.Tests;

public class InstrumentationRunnerTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _adbPath;

    public InstrumentationRunnerTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDirectory);
        _adbPath = Path.Combine(_tempDirectory, "adb");
        File.WriteAllText(_adbPath, string.Empty);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }

        GC.SuppressFinalize(this);
    }

    public static TheoryData<string, string, string> FailedResultScenarios => new()
    {
        {
            "standard xUnit runner",
            "test-results-path",
            """
            <assemblies>
              <assembly name="Tests" total="2" passed="1" failed="1" skipped="0" errors="0" />
            </assemblies>
            """
        },
        {
            "NativeAOT generated xUnit runner",
            "test-results-path",
            """
            <assemblies>
              <assembly name="Tests" test-framework="XUnitWrapperGenerator-generated-runner"
                        total="2" passed="1" failed="1" skipped="0" errors="0">
                <collection name="Collection" total="2" passed="1" failed="1" skipped="0" errors="0" />
              </assembly>
            </assemblies>
            """
        },
        {
            "NUnit v2 runner",
            "nunit2-results-path",
            """<test-results name="Tests" total="2" errors="0" failures="1" not-run="0" />"""
        },
        {
            "NUnit v3 runner",
            "test-results-path",
            """<test-run id="2" testcasecount="2" result="Failed" total="2" passed="1" failed="1" />"""
        },
    };

    [Theory]
    [MemberData(nameof(FailedResultScenarios))]
    public void FailedTestResultsFromSupportedAndroidRunnerPathsOverrideSuccessfulInstrumentation(
        string runnerName,
        string resultPathKey,
        string resultXml)
    {
        string outputDirectory = Path.Combine(_tempDirectory, runnerName.Replace(' ', '-'));
        var processManager = new ResultProducingAdbProcessManager(resultPathKey, resultXml);
        var adbRunner = new AdbRunner(Mock.Of<ILogger>(), processManager, _adbPath);
        var instrumentationRunner = new InstrumentationRunner(Mock.Of<ILogger>(), adbRunner);

        ExitCode exitCode = instrumentationRunner.RunApkInstrumentation(
            apkPackageName: "net.dot.Tests",
            instrumentationName: "net.dot.MonoRunner",
            instrumentationArguments: new Dictionary<string, string>(),
            outputDirectory,
            deviceOutputFolder: null,
            timeout: TimeSpan.FromMinutes(1),
            expectedExitCode: (int)ExitCode.SUCCESS);

        Assert.Equal(ExitCode.TESTS_FAILED, exitCode);
        Assert.True(File.Exists(Path.Combine(outputDirectory, "testResults.xml")));
    }

    private sealed class ResultProducingAdbProcessManager : IAdbProcessManager
    {
        private const string DeviceResultsPath = "/data/user/0/net.dot.Tests/files/testResults.xml";

        private readonly string _resultPathKey;
        private readonly string _resultXml;

        public ResultProducingAdbProcessManager(string resultPathKey, string resultXml)
        {
            _resultPathKey = resultPathKey;
            _resultXml = resultXml;
        }

        public string DeviceSerial { get; set; } = "";

        public ProcessExecutionResults Run(string filename, IEnumerable<string> arguments, TimeSpan timeout)
        {
            string[] args = arguments.ToArray();
            int commandIndex = args[0] == "-s" ? 2 : 0;

            return args[commandIndex] switch
            {
                "shell" => SuccessfulResult(
                    $"INSTRUMENTATION_RESULT: return-code=0{Environment.NewLine}" +
                    $"INSTRUMENTATION_RESULT: {_resultPathKey}={DeviceResultsPath}"),
                "pull" => PullResultFile(args[commandIndex + 2]),
                "logcat" => SuccessfulResult(""),
                _ => throw new InvalidOperationException($"Unexpected fake ADB command: {string.Join(" ", args)}"),
            };
        }

        private ProcessExecutionResults PullResultFile(string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);
            File.WriteAllText(Path.Combine(destinationDirectory, "testResults.xml"), _resultXml);
            return SuccessfulResult("");
        }

        private static ProcessExecutionResults SuccessfulResult(string standardOutput)
            => new()
            {
                ExitCode = (int)AdbExitCodes.SUCCESS,
                StandardOutput = standardOutput,
            };
    }
}
