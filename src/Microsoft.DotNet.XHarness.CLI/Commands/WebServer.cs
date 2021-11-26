// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Microsoft.DotNet.XHarness.CLI.Commands;

public class WebServer
{
    internal static async Task<ServerURLs> Start(IWebServerArguments arguments, string? contentRoot, ILogger logger, Func<WebSocket, Task>? onConsoleConnected, CancellationToken token)
    {
        var urls = arguments.WebServerUseHttps
                ? new string[] { "http://127.0.0.1:0", "https://127.0.0.1:0" }
                : new string[] { "http://127.0.0.1:0" };

        var builder = new WebHostBuilder()
            .UseKestrel()
            .UseStartup<TestWebServerStartup>()
            .ConfigureLogging(logging =>
            {
                logging.AddConsole().AddFilter(null, LogLevel.Warning);
            })
            .ConfigureServices((ctx, services) =>
            {
                if (arguments.WebServerUseCors)
                {
                    services.AddCors(o => o.AddPolicy("AnyCors", builder =>
                        {
                            builder.AllowAnyOrigin()
                                .AllowAnyMethod()
                                .AllowAnyHeader()
                                .WithExposedHeaders("*");
                        }));
                }
                services.AddRouting();
                services.AddSingleton<ILogger>(logger);
                services.Configure<TestWebServerOptions>(ctx.Configuration);
                services.Configure<TestWebServerOptions>(options =>
                {
                    options.WebServerUseCors = arguments.WebServerUseCors;
                    options.WebServerUseCrossOriginPolicy = arguments.WebServerUseCrossOriginPolicy;
                    options.OnConsoleConnected = onConsoleConnected;
                    foreach (var (middlewarePath, middlewareTypeName) in arguments.WebServerMiddlewarePathsAndTypes.Value)
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
            .UseUrls(urls);

        if (contentRoot != null)
        {
            builder.UseContentRoot(contentRoot);
        }

        var host = builder.Build();

        await host.StartAsync(token);

        var ipAddress = host.ServerFeatures
            .Get<IServerAddressesFeature>()?
            .Addresses
            .Where(a => a.StartsWith("http:"))
            .Select(a => new Uri(a))
            .Select(uri => $"{uri.Host}:{uri.Port}")
            .FirstOrDefault();

        var ipAddressSecure = arguments.WebServerUseHttps
            ? host.ServerFeatures
                .Get<IServerAddressesFeature>()?
                .Addresses
                .Where(a => a.StartsWith("https:"))
                .Select(a => new Uri(a))
                .Select(uri => $"{uri.Host}:{uri.Port}")
                .FirstOrDefault()
            : null;

        if (ipAddress == null || (arguments.WebServerUseHttps && ipAddressSecure == null))
        {
            throw new InvalidOperationException("Failed to determine web server's IP address or port");
        }

        return new ServerURLs(ipAddress, ipAddressSecure);
    }

    private class TestWebServerStartup
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
            provider.Mappings[".cjs"] = "text/javascript";
            provider.Mappings[".mjs"] = "text/javascript";

            foreach (var extn in new string[] { ".dll", ".pdb", ".dat", ".blat" })
            {
                provider.Mappings[extn] = "application/octet-stream";
            }

            var options = optionsAccessor.CurrentValue;

            if (options.WebServerUseCrossOriginPolicy)
            {
                app.Use((context, next) =>
                {
                    context.Response.Headers.Add("Cross-Origin-Embedder-Policy", "require-corp");
                    context.Response.Headers.Add("Cross-Origin-Opener-Policy", "same-origin");
                    return next();
                });
            }

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(_hostingEnvironment.ContentRootPath),
                ContentTypeProvider = provider,
                ServeUnknownFileTypes = true
            });

            if (options.WebServerUseCors)
            {
                app.UseCors("AnyCors");
            }
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

    private class TestWebServerOptions
    {
        public Func<WebSocket, Task>? OnConsoleConnected { get; set; }
        public IList<Type> EchoServerMiddlewares { get; set; } = new List<Type>();
        public bool WebServerUseCors { get; set; }
        public bool WebServerUseCrossOriginPolicy { get; set; }
    }
}

public record ServerURLs(string Http, string? Https);
