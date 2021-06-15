// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.Common.CLI.Commands;
using Microsoft.Extensions.Logging;
using Mono.Options;
using Xunit;

#nullable enable
namespace Microsoft.DotNet.XHarness.Common.Tests.Utilities
{
    public class XHarnessCommandTests
    {
        private readonly SampleUnitTestArguments _arguments;
        private readonly UnitTestCommand<SampleUnitTestArguments> _command;

        public XHarnessCommandTests()
        {
            _arguments = new SampleUnitTestArguments();
            _command = new UnitTestCommand<SampleUnitTestArguments>(_arguments, false);
        }

        [Fact]
        public void ArgumentsWithEqualSignsAreParsed()
        {
            var exitCode = _command.Invoke(new[]
            {
                "--number=50",
                "--enum=Value2",
                "--string=foobar",
            });

            Assert.Equal(0, exitCode);
            Assert.True(_command.CommandRun);
            Assert.Equal(50, _arguments.Number);
            Assert.Equal(SampleEnum.Value2, _arguments.Enum);
            Assert.Equal("foobar", _arguments.String);
        }

        [Fact]
        public void ArgumentsWithSpacesAreParsed()
        {
            var exitCode = _command.Invoke(new[]
            {
                "--number",
                "50",
                "--enum",
                "Value2",
                "-s",
                "foobar",
            });

            Assert.Equal(0, exitCode);
            Assert.True(_command.CommandRun);
            Assert.Equal(50, _arguments.Number);
            Assert.Equal(SampleEnum.Value2, _arguments.Enum);
            Assert.Equal("foobar", _arguments.String);
        }

        [Fact]
        public void ArgumentsAreValidated()
        {
            var exitCode = _command.Invoke(new[]
            {
                "-n",
                "200",
                "--enum",
                "Value2",
            });

            Assert.Equal((int)ExitCode.INVALID_ARGUMENTS, exitCode);
            Assert.False(_command.CommandRun);
        }

        [Fact]
        public void VerbosityArgumentIsDetected()
        {
            var exitCode = _command.Invoke(new[]
            {
                "-n",
                "50",
                "--verbosity=Warning",
            });

            Assert.Equal(0, exitCode);
            Assert.True(_command.CommandRun);
            Assert.Equal(50, _arguments.Number);
            Assert.Equal(LogLevel.Warning, _arguments.Verbosity);
        }

        [Fact]
        public void HelpArgumentIsDetected()
        {
            var exitCode = _command.Invoke(new[]
            {
                "--help",
            });

            Assert.Equal((int)ExitCode.HELP_SHOWN, exitCode);
            Assert.False(_command.CommandRun);
            Assert.True(_arguments.ShowHelp);
        }

        [Fact]
        public void ExtraneousArgumentsAreRejected()
        {
            var exitCode = _command.Invoke(new[]
            {
                "-n",
                "50",
                "--enum",
                "Value2",
                "--invalid-arg=foo",
            });

            Assert.Equal((int)ExitCode.INVALID_ARGUMENTS, exitCode);
            Assert.False(_command.CommandRun);
        }

        [Fact]
        public void ExtraneousArgumentsAreDetected()
        {
            var arguments = new SampleUnitTestArguments();
            var command = new UnitTestCommand<SampleUnitTestArguments>(arguments, true);
            var exitCode = command.Invoke(new[]
            {
                "-n",
                "50",
                "--enum",
                "Value2",
                "some",
                "other=1",
                "args",
            });

            Assert.Equal(0, exitCode);
            Assert.True(command.CommandRun);
            Assert.Equal(50, arguments.Number);
            Assert.Equal(SampleEnum.Value2, arguments.Enum);
            Assert.Equal(new[] { "some", "other=1", "args" }, command.ExtraArgs);
        }

        [Fact]
        public void EnumsAreValidated()
        {
            var exitCode = _command.Invoke(new[]
            {
                "--enum",
                "Foo",
            });

            Assert.Equal((int)ExitCode.INVALID_ARGUMENTS, exitCode);
            Assert.False(_command.CommandRun);
        }

        [Fact]
        public void ForbiddenEnumValuesAreValidated()
        {
            var exitCode = _command.Invoke(new[]
            {
                "--enum",
                "ForbiddenValue",
            });

            Assert.Equal((int)ExitCode.INVALID_ARGUMENTS, exitCode);
            Assert.False(_command.CommandRun);
        }

        [Fact]
        public void PassThroughArgumentsAreParsed()
        {
            var exitCode = _command.Invoke(new[]
            {
                "-n",
                "50",
                "--enum",
                "Value2",
                XHarnessCommand.VerbatimArgumentPlaceholder,
                "v8",
                "--foo",
                "runtime.js",
            });

            Assert.Equal(0, exitCode);
            Assert.True(_command.CommandRun);
            Assert.Equal(50, _arguments.Number);
            Assert.Equal(SampleEnum.Value2, _arguments.Enum);
            Assert.Equal(new[] { "v8", "--foo", "runtime.js" }, _command.PassThroughArgs.ToArray());
        }

        [Fact]
        public void PassThroughArgumentsAreParsedInCommandSet()
        {
            var arguments = new SampleUnitTestArguments();
            var command = new UnitTestCommand<SampleUnitTestArguments>(arguments, false);
            var commandSet = new CommandSet("set")
            {
                command
            };

            var exitCode = commandSet.Run(new[]
            {
                "unit-test",
                "-n",
                "50",
                "--enum",
                "Value2",
                XHarnessCommand.VerbatimArgumentPlaceholder,
                "v8",
                "--foo",
                "runtime.js",
            });

            Assert.Equal(0, exitCode);
            Assert.True(command.CommandRun);
            Assert.Equal(50, arguments.Number);
            Assert.Equal(SampleEnum.Value2, arguments.Enum);
            Assert.Equal(new[] { "v8", "--foo", "runtime.js" }, command.PassThroughArgs.ToArray());
        }

        [Fact]
        public void ExtraneousArgumentsDetectedInPassThroughMode()
        {
            var exitCode = _command.Invoke(new[]
            {
                "v8",
                "--foo",
                "runtime.js",
                XHarnessCommand.VerbatimArgumentPlaceholder,
                "-n",
                "50",
                "--enum",
                "--invalid-arg=foo",
            });

            Assert.Equal((int)ExitCode.INVALID_ARGUMENTS, exitCode);
            Assert.False(_command.CommandRun);
        }

        private class SampleUnitTestArguments : XHarnessCommandArguments
        {
            public SampleNumberArgument Number { get; } = new();
            public SampleEnumArgument Enum { get; } = new();
            public SampleStringArgument String { get; } = new();

            protected override IEnumerable<Argument> GetArguments() => new Argument[]
            {
                Number,
                Enum,
                String,
            };
        }

        private enum SampleEnum
        {
            Value1,
            Value2,
            ForbiddenValue,
        }

        private class SampleNumberArgument : IntArgument
        {
            public SampleNumberArgument()
                : base("number=|n=", "Sets the number, should be less than 100")
            {
            }

            public override void Validate()
            {
                base.Validate();

                if (Value > 100)
                {
                    throw new ArgumentOutOfRangeException();
                }
            }
        }

        private class SampleEnumArgument : Argument<SampleEnum>
        {
            public SampleEnumArgument()
                : base("enum=|e=", "Sets the enum", SampleEnum.Value1)
            {
            }

            public override void Action(string argumentValue) => Value = ParseArgument("enum", argumentValue, SampleEnum.ForbiddenValue);
        }

        private class SampleStringArgument : StringArgument
        {
            public SampleStringArgument()
                : base("string=|s=", "Sets the string")
            {
            }
        }
    }
}
