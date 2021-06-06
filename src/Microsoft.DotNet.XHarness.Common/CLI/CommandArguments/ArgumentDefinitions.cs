// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.Common.CLI.CommandArguments
{
    public class ArgumentDefinitions
    {
        private readonly Dictionary<Type, List<ArgumentDefinition>> _arguments = new();

        public void Add<T>(string prototype, string description, Action<string, T> action)
        {
            var t = typeof(T);
            var definition = new ArgumentDefinition<T>(prototype, description, action);

            if (_arguments.TryGetValue(t, out var definitions))
            {
                definitions.Add(definition);
            }
            else
            {
                _arguments.Add(t, new List<ArgumentDefinition> { definition });
            }
        }

        public IEnumerable<Option> BindOptions<T>(T commandArguments) where T : class
        {
            var definitions = _arguments[typeof(T)];
            var set = new OptionSet();

            foreach (var def in definitions)
            {
                // We know this cast will succeed because we control the creation
                var definition = (ArgumentDefinition<T>)def;
                set.Add(definition.Prototype, definition.Description, v => definition.Action(v, commandArguments));
            }

            return set;
        }

        private abstract class ArgumentDefinition
        {
        }

        private class ArgumentDefinition<T> : ArgumentDefinition
        {
            public string Prototype { get; }
            public string Description { get; }
            public Action<string, T> Action { get; }

            public ArgumentDefinition(string prototype, string description, Action<string, T> action)
            {
                Prototype = prototype;
                Description = description;
                Action = action;
            }
        }
    }
}
