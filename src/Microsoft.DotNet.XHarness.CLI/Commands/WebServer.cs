// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net.WebSockets;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using System.Runtime.Loader;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.DotNet.XHarness.CLI.CommandArguments;

namespace Microsoft.DotNet.XHarness.CLI.Commands
{
    public class WebServer
    {
        internal static async Task<ServerURLs> Start(TestCommandArguments arguments, ILogger logger, Func<WebSocket, Task>? onConsoleConnected, CancellationToken token)
        {
            var host = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(arguments.AppPackagePath)
                .UseStartup<TestWebServerStartup>()
                .ConfigureLogging(logging =>
                {
                    logging.AddConsole().AddFilter(null, LogLevel.Warning);
                })
                .ConfigureServices((ctx, services) =>
                {
                    services.AddRouting();
                    services.AddSingleton<ILogger>(logger);
                    services.Configure<TestWebServerOptions>(ctx.Configuration);
                    services.Configure<TestWebServerOptions>(options =>
                    {
                        options.OnConsoleConnected = onConsoleConnected;
                        foreach (var (middlewarePath, middlewareTypeName) in arguments.WebServerMiddlewarePathsAndTypes)
                        {
                            var extensionAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(middlewarePath);
                            var middlewareType = extensionAssembly?.GetTypes().Where(type => type.Name == middlewareTypeName).FirstOrDefault();
                            if (middlewareType == null)
                            {
                                var message = $"Can't find {middlewareTypeName} middleware in {middlewarePath}";
                                logger.LogError(message);
                                throw new Exception(message);
                            }
                            options.EchoServerMiddlewares.Add(middlewareType);
                        }
                    });
                })
                .UseUrls("http://127.0.0.1:0", "https://127.0.0.1:0")
                .Build();

            await host.StartAsync(token);

            var ipAddress = host.ServerFeatures
                .Get<IServerAddressesFeature>()?
                .Addresses
                .Where(a => a.StartsWith("http:"))
                .Select(a => new Uri(a))
                .Select(uri => $"{uri.Host}:{uri.Port}")
                .FirstOrDefault();

            var ipAddressSecure = host.ServerFeatures
                .Get<IServerAddressesFeature>()?
                .Addresses
                .Where(a => a.StartsWith("https:"))
                .Select(a => new Uri(a))
                .Select(uri => $"{uri.Host}:{uri.Port}")
                .FirstOrDefault();

            if (ipAddress == null || ipAddressSecure == null)
            {
                throw new InvalidOperationException("Failed to determine web server's IP address or port");
            }

            return new ServerURLs(ipAddress, ipAddressSecure);
        }

        class TestWebServerStartup
        {
            private readonly IWebHostEnvironment _hostingEnvironment;
            private readonly ILogger _logger;

            public TestWebServerStartup(IWebHostEnvironment hostingEnvironment, ILogger logger)
            {
                _hostingEnvironment = hostingEnvironment;
                _logger = logger;
            }

            public void Configure(IApplicationBuilder app, IOptionsMonitor<TestWebServerOptions> optionsAccessor)
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

                app.UseWebSockets();
                if (options.OnConsoleConnected != null)
                {
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
                }

                foreach (var middleware in options.EchoServerMiddlewares)
                {
                    app.UseMiddleware(middleware);
                    _logger.LogInformation($"Loaded {middleware.FullName} middleware");
                }
            }
        }

        class TestWebServerOptions
        {
            public Func<WebSocket, Task>? OnConsoleConnected { get; set; }
            public IList<Type> EchoServerMiddlewares { get; set; } = new List<Type>();
        }
    }

    public record ServerURLs(string Http, string Https);
}