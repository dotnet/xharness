using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Wasm
{
    public class WasmTestWebServerStartup
    {
        public void Configure(IApplicationBuilder app)
        {
            var provider = new FileExtensionContentTypeProvider ();
            provider.Mappings [".wasm"] = "application/wasm";

            foreach (var extn in new string[] { ".dll", ".pdb", ".dat", ".blat" })
            {
                provider.Mappings [extn] = "application/octet-stream";
            }

            app.UseStaticFiles (new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider (Directory.GetCurrentDirectory ()),
                ContentTypeProvider = provider
            });
        }
    }
}
