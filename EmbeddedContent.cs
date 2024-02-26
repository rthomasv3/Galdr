using System;
using System.Linq;
using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SharpWebview.Content;

namespace Galdr;

internal sealed class EmbeddedContent : IWebviewContent, IDisposable
{
    #region Fields

    private readonly WebApplication _webApp;

    #endregion

    #region Constructor

    public EmbeddedContent(int port = 0)
    {
        WebApplicationBuilder webApplicationBuilder = WebApplication.CreateBuilder();
        webApplicationBuilder.WebHost.UseKestrel(delegate (KestrelServerOptions options)
        {
            options.Listen(IPAddress.Loopback, port);
        });

        _webApp = webApplicationBuilder.Build();

        Assembly assembly = Assembly.GetEntryAssembly();
        EmbeddedFileProvider embeddedFileProvider = new(assembly, $"{assembly.EntryPoint.DeclaringType.Namespace}.dist");
        FileServerOptions fileServerOptions = new FileServerOptions
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

    public void Dispose()
    {
        _webApp.DisposeAsync();
    }

    public string ToWebviewUrl()
    {
        return _webApp.Urls.First().Replace("127.0.0.1", "localhost");
    }

    #endregion
}
