using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharpWebview.Content;

namespace Galdr.Native;

/// <summary>
/// Class used to serve content from a local dist folder.
/// </summary>
public sealed class LocalHostedContent : IWebviewContent
{
    private readonly WebApplication _webApp;

    /// <summary>
    /// Creates a new instance of the <see cref="LocalHostedContent"/> class.
    /// </summary>
    /// <param name="port">The port to serve on (0 for auto-select)</param>
    /// <param name="activateLog">True if you want to clear logging providers</param>
    /// <param name="additionalMimeTypes">Additional mime types to add to the file server options.</param>
    public LocalHostedContent(int port = 0, bool activateLog = false, IDictionary<string, string> additionalMimeTypes = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, port));

        if (!activateLog)
        {
            builder.Logging.ClearProviders();
        }

        _webApp = builder.Build();

        FileServerOptions fileServerOptions = new FileServerOptions
        {
            FileProvider = new PhysicalFileProvider(Path.Combine(System.Environment.CurrentDirectory, "dist")),
            RequestPath = "",
            EnableDirectoryBrowsing = true,
        };

        if (additionalMimeTypes != null)
        {
            FileExtensionContentTypeProvider extensionProvider = new FileExtensionContentTypeProvider();

            foreach (var mimeType in additionalMimeTypes)
            {
                extensionProvider.Mappings.Add(mimeType);
            }

            fileServerOptions.StaticFileOptions.ContentTypeProvider = extensionProvider;
        }

        _webApp.UseFileServer(fileServerOptions);
        _webApp.Start();
    }

    /// <summary>
    /// Returns the URL of the <see cref="WebApplication"/>.
    /// </summary>
    public string ToWebviewUrl()
    {
        return _webApp.Urls.First().Replace("127.0.0.1", "localhost");
    }
}
