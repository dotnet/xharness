// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments
{
    internal abstract class XHarnessCommandArguments
    {
        public LogLevel Verbosity { get; set; } = LogLevel.Information;
        public bool ShowHelp { get; set; } = false;

        public virtual OptionSet GetOptions() => new OptionSet
        {
            {
                "verbosity=|v=",
                "Verbosity level (1-6) where higher means less logging. (default = 2 / Information)",
                v =>
                {
                    Verbosity = ParseArgument<LogLevel>("verbosity", v);
                }
            },
            { "help|h", v => ShowHelp = v != null }
        };

        protected static string RootPath(string path)
        {
            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(Directory.GetCurrentDirectory(), path);
            }

            return path;
        }

        protected static TEnum ParseArgument<TEnum>(string argumentName, string? value, params TEnum[]? invalidValues) where TEnum : struct, IConvertible
        {
            var type = typeof(TEnum);

            if (!type.IsEnum)
            {
                throw new ArgumentException(nameof(TEnum) + " must be an enumerated type");
            }

            if (Enum.TryParse(value, ignoreCase: true, out TEnum result))
            {
                if (invalidValues != null && invalidValues.Contains(result))
                {
                    throw new ArgumentException($"{result} is an invalid value for {argumentName}");
                }

                return result;
            }

            IEnumerable<TEnum> validOptions = Enum.GetValues(type).Cast<TEnum>();

            if (invalidValues != null)
            {
                validOptions = validOptions.Where(v => !invalidValues.Contains(v));
            }

            throw new ArgumentException(
                $"Invalid value '{value}' supplied in {argumentName}. " +
                $"Valid values are:" + GetAllowedValues(invalidValues: invalidValues));
        }

        protected static string GetAllowedValues<TEnum>(Func<TEnum, string>? display = null, params TEnum[]? invalidValues) where TEnum : struct, IConvertible
        {
            var values = Enum.GetValues(typeof(TEnum)).Cast<TEnum>();

            if (invalidValues != null)
            {
                values = values.Where(v => !invalidValues.Contains(v));
            }

            return Environment.NewLine + "\t- " + string.Join($"{Environment.NewLine}\t- ", values.Select(t => display?.Invoke(t) ?? t.ToString()));
        }

        public abstract void Validate();
    }
}
