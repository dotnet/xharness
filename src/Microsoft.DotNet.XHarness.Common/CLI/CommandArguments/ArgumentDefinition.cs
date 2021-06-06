// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

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

        public abstract void Action(string argumentValue);

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
    }

    public abstract class StringArgument : ArgumentDefinition
    {
        public string? Value { get; private set; }

        public StringArgument(string prototype, string description)
            : base(prototype, description)
        {
        }

        public override void Action(string argumentValue) => Value = argumentValue;
    }

    public abstract class TimeoutArgument : ArgumentDefinition
    {
        protected TimeoutArgument(string prototype, string description) : base(prototype, description)
        {
        }

        public TimeSpan? Timeout { get; set; }

        public override void Action(string argumentValue)
        {
            if (int.TryParse(argumentValue, out var timeout))
            {
                Timeout = TimeSpan.FromSeconds(timeout);
                return;
            }

            if (TimeSpan.TryParse(argumentValue, out var timespan))
            {
                Timeout = timespan;
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
    }

    public abstract class SwitchArgument : ArgumentDefinition
    {
        public bool Value { get; private set; }

        public SwitchArgument(string prototype, string description)
            : base(prototype, description)
        {
        }

        public override void Action(string argumentValue)
            => Value = string.IsNullOrEmpty(argumentValue)
                ? true
                : argumentValue.Equals("false", StringComparison.InvariantCultureIgnoreCase);
    }
}
