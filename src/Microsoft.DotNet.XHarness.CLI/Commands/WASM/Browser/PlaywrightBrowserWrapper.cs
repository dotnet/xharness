// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Wasm;

/// <summary>
/// Wrapper around Playwright IPage to provide Selenium-like interface for easier migration
/// </summary>
internal class PlaywrightBrowserWrapper : IDisposable
{
    private readonly IPage _page;
    private readonly IBrowser _browser;
    private readonly IPlaywright _playwright;

    public PlaywrightBrowserWrapper(IPage page, IBrowser browser, IPlaywright playwright)
    {
        _page = page;
        _browser = browser;
        _playwright = playwright;
    }

    public IPage Page => _page;

    public async Task NavigateToUrlAsync(string url)
    {
        await _page.GotoAsync(url);
    }

    public async Task<string> FindElementTextAsync(string selector)
    {
        var element = await _page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions 
        { 
            Timeout = 30000 
        });
        return await element!.InnerTextAsync();
    }

    public void Dispose()
    {
        _page?.CloseAsync().Wait();
        _browser?.CloseAsync().Wait();
        _playwright?.Dispose();
    }
}

/// <summary>
/// Service wrapper to mimic Selenium DriverService behavior
/// </summary>
internal class PlaywrightServiceWrapper : IDisposable
{
    private readonly IBrowser _browser;
    public bool IsRunning => !_browser.IsConnected || _browser.IsConnected;

    public PlaywrightServiceWrapper(IBrowser browser)
    {
        _browser = browser;
    }

    public void Dispose()
    {
        _browser?.CloseAsync().Wait();
    }
}
