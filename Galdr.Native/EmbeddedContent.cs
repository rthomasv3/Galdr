using System;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SharpWebview.Content;

namespace Galdr.Native;

/// <summary>
/// Class used to serve content embedded in the assembly.
/// </summary>
public sealed class EmbeddedContent : IWebviewContent, IAsyncDisposable
{
    #region Fields

    private readonly WebApplication _webApp;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new instance of the <see cref="EmbeddedContent"/> class.
    /// </summary>
    /// <param name="port">The port to serve on (0 for auto-select)</param>
    /// <param name="embeddedNamespace">The namespace where embedded resources are located (e.g., "MyApp.dist")</param>
    /// <param name="assembly">The assembly containing the embedded resources (defaults to calling assembly)</param>
    /// <param name="contentDir">The directory of the content to serve (defaults to "dist")</param>
    public EmbeddedContent(int port = 0, string embeddedNamespace = null, Assembly assembly = null, string contentDir = "dist")
    {
        WebApplicationBuilder webApplicationBuilder = WebApplication.CreateBuilder();
        webApplicationBuilder.WebHost.UseKestrel(delegate (KestrelServerOptions options)
        {
            options.Listen(IPAddress.Loopback, port);
        });

        _webApp = webApplicationBuilder.Build();

        // Use provided assembly or default to the entry assembly
        Assembly targetAssembly = assembly ?? Assembly.GetEntryAssembly();

        // Use provided namespace or construct default
        string targetNamespace = $"{embeddedNamespace ?? targetAssembly.GetName().Name}.{contentDir}";

        EmbeddedFileProvider embeddedFileProvider = new(targetAssembly, targetNamespace);
        FileServerOptions fileServerOptions = new()
        {
            FileProvider = embeddedFileProvider,
            RequestPath = "",
            EnableDirectoryBrowsing = true
        };
        _webApp.UseFileServer(fileServerOptions);

        _webApp.Start();
    }

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return _webApp?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    /// <summary>
    /// Returns the URL of the <see cref="WebApplication"/>.
    /// </summary>
    public string ToWebviewUrl()
    {
        return _webApp.Urls.First().Replace("127.0.0.1", "localhost");
    }

    #endregion
}
