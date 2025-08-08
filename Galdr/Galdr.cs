using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using SharpWebview;
using SharpWebview.Content;

[assembly: InternalsVisibleTo("Galdr.Tests")]

namespace Galdr;

/// <summary>
/// Class used to create a <see cref="Webview"/> and handle interactions between the frontend and backend.
/// </summary>
public class Galdr : IDisposable
{
    #region Fields

    private readonly Webview _webView;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, MethodInfo> _commands;
    private readonly IWebviewContent _mainContent;
    private readonly ExecutionService _executionService;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new instance of the <see cref="Galdr"/> class.
    /// </summary>
    /// <remarks>
    /// Requires the threading model for the application to be single-threaded apartment (<see cref="STAThreadAttribute"/>).
    /// </remarks>
    /// <exception cref="NullReferenceException">
    /// </exception>
    /// <exception cref="AccessViolationException">
    /// Thrown when the threading model for the application is not single-threaded apartment (<see cref="STAThreadAttribute"/>).
    /// </exception>
    public Galdr(GaldrOptions options)
    {
        _commands = options.Commands;

        _mainContent = GetContent(options.Port);

        IWebviewContent loadingContent = options.ShowLoading ?
            new LoadingContent(options.LoadingMessage, options.LoadingBackground) :
            _mainContent;

        _webView = new Webview(options.Debug, true)
            .SetTitle(options.Title)
            .Bind("galdrInvoke", HandleCommand)
            .Navigate(loadingContent);

        _serviceProvider = options.Services
            .AddTransient(_ => new EventService(_webView))
            .AddTransient<IEventService, EventService>()
            .BuildServiceProvider();

        _executionService = new(_serviceProvider);

        if (options.ShowLoading)
        {
            Task.Run(async () =>
            {
                await WaitForMainContentReady();

                _webView.Dispatch(async () =>
                {
                    _webView.Navigate(_mainContent);

                    await Task.Delay(250);

                    _webView.SetSize(options.Width, options.Height, WebviewHint.None);
                    _webView.SetSize(options.MinWidth, options.MinHeight, WebviewHint.Min);
                });
            });
        }
        else
        {
            _webView.SetSize(options.Width, options.Height, WebviewHint.None);
            _webView.SetSize(options.MinWidth, options.MinHeight, WebviewHint.Min);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Runs the main loop of the <see cref="Webview"/>.
    /// </summary>
    public Galdr Run()
    {
        _webView.Run();
        return this;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_mainContent is IDisposable disposableContent)
        {
            disposableContent.Dispose();
        }
        else if (_mainContent is IAsyncDisposable asyncDisposableContent)
        {
            asyncDisposableContent.DisposeAsync();
        }

        _webView.Dispose();
    }

    #endregion

    #region Private Methods

    private IWebviewContent GetContent(int port)
    {
        bool serverIsRunning = false;
        string url = $"http://localhost:{port}";

        try
        {
            using HttpClient client = new();
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(250);
            _ = client.GetAsync(url, tokenSource.Token).Result;
            serverIsRunning = true;
        }
        catch { }

        return serverIsRunning ? new UrlContent(url) : new EmbeddedContent(port);
    }

    private async void HandleCommand(string id, string paramString)
    {
        object[] parameters = JsonConvert.DeserializeObject<object[]>(paramString);

        if (parameters.Length > 0)
        {
            string commandName = parameters[0].ToString();

            if (!String.IsNullOrWhiteSpace(commandName) && _commands.ContainsKey(commandName))
            {
                try
                {
                    MethodInfo method = _commands[commandName];

                    object[] args = _executionService.ExtractArguments(method, parameters.Skip(1));
                    object result = await _executionService.ExecuteMethod(method, args);

                    if (result != null)
                    {
                        _webView.Return(id, RPCResult.Success, JsonConvert.SerializeObject(result));
                    }
                    else
                    {
                        _webView.Return(id, RPCResult.Success, "");
                    }
                }
                catch (Exception e)
                {
                    _webView.Return(id, RPCResult.Error, JsonConvert.SerializeObject(e));
                }
            }
        }
    }

    private async Task WaitForMainContentReady()
    {
        await Task.Delay(250);

        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(3);
            await httpClient.GetAsync(_mainContent.ToWebviewUrl());
        }
        catch { }
    }

    #endregion
}
