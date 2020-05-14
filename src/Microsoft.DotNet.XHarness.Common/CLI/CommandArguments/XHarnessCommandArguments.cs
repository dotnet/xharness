// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.Common.CLI.CommandArguments
{
    internal abstract class XHarnessCommandArguments
    {
        public LogLevel Verbosity { get; set; } = LogLevel.Information;
        public bool ShowHelp { get; set; } = false;

        /// <summary>
        /// Collects together all options from this class and from GetCommandOptions()
        /// Options from this class are appended to the bottom of the list.
        /// </summary>
        /// <remarks>
        /// If you don't want the verbosity option, feel free to override this method
        /// but the help option is important for the help command to work correctly.
        /// </remarks>
        public OptionSet GetOptions()
        {
            var options = GetCommandOptions();

            options.Add("verbosity:|v:", "Verbosity level - defaults to 'Information' if not specified. If passed without value, 'Debug' is assumed (highest)",
                v => Verbosity = string.IsNullOrEmpty(v) ? LogLevel.Debug : ParseArgument<LogLevel>("verbosity", v));

            options.Add("help|h", v => ShowHelp = v != null);

            return options;
        }

        /// <summary>
        /// Allows to implement additional validation (e.g. "directory exists?").
        /// Should throw an ArgumentException if validation fails.
        /// </summary>
        public abstract void Validate();

        /// <summary>
        /// Returns additional option for your specific command.
        /// </summary>
        protected abstract OptionSet GetCommandOptions();

        protected static string RootPath(string path)
        {
            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(Directory.GetCurrentDirectory(), path);
            }

            return path;
        }

        /// <summary>
        /// Helper method that enables parsing of enums from string.
        /// When an invalid value is supplied, available values are printed.
        /// </summary>
        /// <typeparam name="TEnum">Enum type</typeparam>
        /// <param name="argumentName">Name of the argument that is being parsed, strictly for help printing purposes</param>
        /// <param name="value">Value of the arg to be parsed</param>
        /// <param name="invalidValues">List of values that should not be available to set</param>
        /// <returns>Parsed enum value</returns>
        protected static TEnum ParseArgument<TEnum>(string argumentName, string? value, params TEnum[]? invalidValues) where TEnum : struct, IConvertible
        {
            if (value == null)
            {
                throw new ArgumentNullException(message: $"Empty value supplied to {argumentName}", null);
            }

            if (value.All(c => char.IsDigit(c)))
            {
                // Any int would parse into enum successfully, so we forbid that
                throw new ArgumentException(
                    $"Invalid value '{value}' supplied for {argumentName}. " +
                    $"Valid values are:" + GetAllowedValues(invalidValues: invalidValues));
            }

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

        /// <summary>
        /// Helper method that returns a bullet list of available enum values ready for printing to console.
        /// </summary>
        /// <typeparam name="TEnum">Enum type</typeparam>
        /// <param name="display">How to print each enum value. Default is ToString()</param>
        /// <param name="invalidValues">List of values that should not be available to set and are not listed then</param>
        /// <returns>Print-ready list of allowed values</returns>
        protected static string GetAllowedValues<TEnum>(Func<TEnum, string>? display = null, params TEnum[]? invalidValues) where TEnum : struct, IConvertible
        {
            var values = Enum.GetValues(typeof(TEnum)).Cast<TEnum>();

            if (invalidValues != null)
            {
                values = values.Where(v => !invalidValues.Contains(v));
            }

            var separator = Environment.NewLine + "\t- ";
            IEnumerable<string?> allowedValued = values.Select(t => display?.Invoke(t) ?? t.ToString());

            return separator + string.Join(separator, allowedValued);
        }
    }
}
