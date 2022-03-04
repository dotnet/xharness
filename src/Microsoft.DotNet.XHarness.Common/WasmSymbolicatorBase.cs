// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging;

#nullable enable
namespace Microsoft.DotNet.XHarness.Common;

public abstract class WasmSymbolicatorBase
{
    public string? SymbolsFile { get; private set; }
    public string? SymbolsPatternFile { get; private set; }

    public virtual bool Init(string? symbolMapFile, string? symbolsPatternFile, ILogger logger)
    {
        SymbolsFile = symbolMapFile;
        SymbolsPatternFile = symbolsPatternFile;

        return true;
    }

    public static WasmSymbolicatorBase? Create(Type? symbolicatorType, string? symbolMapFile, string? symbolsPatternFile, ILogger logger)
    {
        if (symbolicatorType is null && symbolMapFile is null && symbolsPatternFile is null)
            return null;

        if (symbolicatorType is null)
        {
            logger.LogWarning($"No symbolicator given");
            return null;
        }

        var symbolicator = Activator.CreateInstance(symbolicatorType) as WasmSymbolicatorBase;
        if (symbolicator?.Init(symbolMapFile, symbolsPatternFile, logger) == true)
            return symbolicator;

        return null;
    }

    public abstract string Symbolicate(string msg);
}
