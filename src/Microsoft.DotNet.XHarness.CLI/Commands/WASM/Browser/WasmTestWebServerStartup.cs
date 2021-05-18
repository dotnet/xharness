// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

using System;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Collections.Generic;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Wasm
{
    public class WasmTestWebServerStartup
    {
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly ILogger _logger;

        public WasmTestWebServerStartup(IWebHostEnvironment hostingEnvironment, ILogger logger)
        {
            _hostingEnvironment = hostingEnvironment;
            _logger = logger;
        }

        public void Configure(IApplicationBuilder app, IOptionsMonitor<WasmTestWebServerOptions> optionsAccessor)
        {
            var provider = new FileExtensionContentTypeProvider();
            provider.Mappings[".wasm"] = "application/wasm";

            foreach (var extn in new string[] { ".dll", ".pdb", ".dat", ".blat" })
            {
                provider.Mappings[extn] = "application/octet-stream";
            }

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(_hostingEnvironment.ContentRootPath),
                ContentTypeProvider = provider,
                ServeUnknownFileTypes = true
            });

            var options = optionsAccessor.CurrentValue;
            if (options.OnConsoleConnected == null)
            {
                throw new ArgumentException("Bug: OnConsoleConnected callback not set");
            }

            app.UseWebSockets();
            app.UseRouter(router =>
            {
                router.MapGet("/console", async context =>
                {
                    if (!context.WebSockets.IsWebSocketRequest)
                    {
                        context.Response.StatusCode = 400;
                        return;
                    }

                    var socket = await context.WebSockets.AcceptWebSocketAsync();
                    await options.OnConsoleConnected(socket);
                });
            });
            foreach(var middleware in options.EchoServerMiddlewares){
                app.UseMiddleware(middleware);
            }
        }
    }

    public class WasmTestWebServerOptions
    {
        public Func<WebSocket, Task>? OnConsoleConnected { get; set; }
        public IList<Type> EchoServerMiddlewares { get; set; } = new List<Type>();
    }
}
