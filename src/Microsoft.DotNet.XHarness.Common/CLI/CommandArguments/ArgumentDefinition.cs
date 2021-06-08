// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.XHarness.Common.CLI.CommandArguments
{
    public abstract class ArgumentDefinition
    {
        public string Prototype { get; }

        public string Description { get; }

        protected ArgumentDefinition(string prototype, string description)
        {
            Prototype = prototype;
            Description = description;
        }

        /// <summary>
        /// Action invoked when argument is found.
        /// </summary>
        public abstract void Action(string argumentValue);

        /// <summary>
        /// Allows to implement additional validation (e.g. "directory exists?").
        /// Should throw an ArgumentException if validation fails.
        /// </summary>
        public virtual void Validate()
        {
        }

        protected string RootPath(string path)
        {
            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(Directory.GetCurrentDirectory(), path);
            }

            return path;
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
    }

    public abstract class IntArgument : ArgumentDefinition
    {
        public int Value { get; private set; }

        public IntArgument(string prototype, string description, int defaultValue = 0)
            : base(prototype, description)
        {
            Value = defaultValue;
        }

        public override void Action(string argumentValue)
        {
            if (int.TryParse(argumentValue, out var number))
            {
                Value = number;
                return;
            }

            throw new ArgumentException($"{Prototype} must be an integer");
        }

        public static implicit operator int(IntArgument arg) => arg.Value;
    }

    public abstract class StringArgument : ArgumentDefinition
    {
        public string? Value { get; private set; }

        public StringArgument(string prototype, string description)
            : base(prototype, description)
        {
        }

        public override void Action(string argumentValue) => Value = argumentValue;

        public static implicit operator string?(StringArgument arg) => arg.Value;
    }

    public abstract class TimeSpanArgument : ArgumentDefinition
    {
        protected TimeSpanArgument(string prototype, string description, TimeSpan? defaultValue)
            : base(prototype, description)
        {
            Value = defaultValue;
        }

        public TimeSpan? Value { get; set; }

        public override void Action(string argumentValue)
        {
            if (int.TryParse(argumentValue, out var timeout))
            {
                Value = TimeSpan.FromSeconds(timeout);
                return;
            }

            if (TimeSpan.TryParse(argumentValue, out var timespan))
            {
                Value = timespan;
                return;
            }

            throw new ArgumentException($"{Prototype} must be an integer - a number of seconds, or a timespan (00:30:00)");
        }
    }

    public abstract class PathArgument : ArgumentDefinition
    {
        protected PathArgument(string prototype, string description) : base(prototype, description)
        {
        }

        public string? Path { get; set; }

        public override void Action(string argumentValue) => Path = RootPath(argumentValue);

        public static implicit operator string?(PathArgument arg) => arg.Path;
    }

    public abstract class SwitchArgument : ArgumentDefinition
    {
        public bool Value { get; private set; }

        public SwitchArgument(string prototype, string description, bool defaultValue)
            : base(prototype, description)
        {
            Value = defaultValue;
        }

        public override void Action(string argumentValue)
            => Value = string.IsNullOrEmpty(argumentValue) || argumentValue.Equals("false", StringComparison.InvariantCultureIgnoreCase);

        public static implicit operator bool(SwitchArgument arg) => arg.Value;
    }

    public abstract class RepetableArgument : ArgumentDefinition
    {
        private readonly List<string> _values = new();

        protected RepetableArgument(string prototype, string description) : base(prototype, description)
        {
        }

        public IEnumerable<string> Values => _values;

        public override void Action(string argumentValue) => _values.Add(argumentValue);
    }
}
