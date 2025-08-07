using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using SharpWebview;
using SharpWebview.Content;

namespace Galdr.Native;

/// <summary>
/// Class used to create a <see cref="Webview"/> and handle interactions between the frontend and backend.
/// </summary>
public class Galdr : IDisposable
{
    #region Fields

    private readonly Webview _webView;
    private readonly Dictionary<string, CommandInfo> _commands;
    private readonly IWebviewContent _mainContent;

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

        _mainContent = options.ContentProvider ?? new LocalHostedContent(options.Port);

        IWebviewContent loadingContent = options.ShowLoading ? 
            new LoadingContent(options.LoadingMessage, options.LoadingBackground) : 
            _mainContent;

        _webView = new Webview(options.Debug, true)
            .SetTitle(options.Title)
            .Bind("galdrInvoke", HandleCommand)
            .Navigate(loadingContent);

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

    private async void HandleCommand(string id, string paramString)
    {
        (string commandName, string parameters) = ExtractCommandAndArguments(paramString);

        if (_commands.TryGetValue(commandName, out var commandInfo))
        {
            object result = await commandInfo.Handler(parameters);

            if (result == null)
            {
                _webView.Return(id, RPCResult.Success, "");
            }
            else
            {
                if (GaldrJsonSerializerRegistry.TrySerialize(result, commandInfo.ResultType, out string json))
                {
                    _webView.Return(id, RPCResult.Success, json);
                }
                else
                {
                    _webView.Return(id, RPCResult.Success, result.ToString());
                }
            }
        }
    }

    private (string, string) ExtractCommandAndArguments(string paramString)
    {
        var trimmed = paramString.Trim();

        // Validate it's an array
        if (!trimmed.StartsWith('[') || !trimmed.EndsWith(']'))
            throw new ArgumentException("Invalid JSON array");

        // Remove outer brackets
        var content = trimmed.Substring(1, trimmed.Length - 2).Trim();

        // Find the first string (command name)
        if (!content.StartsWith('"'))
            throw new ArgumentException("Expected command name as first element");

        // Extract command name
        var commandEndIndex = 1;
        while (commandEndIndex < content.Length &&
               (content[commandEndIndex] != '"' || content[commandEndIndex - 1] == '\\'))
        {
            commandEndIndex++;
        }

        var commandName = content.Substring(1, commandEndIndex - 1);

        // Move past the closing quote
        var index = commandEndIndex + 1;

        // Skip whitespace and comma
        while (index < content.Length && (char.IsWhiteSpace(content[index]) || content[index] == ','))
            index++;

        // If we're at the end, no parameters
        if (index >= content.Length)
            return (commandName, "{}");

        // Extract the parameters object
        var parameters = content.Substring(index).Trim();

        // If no parameters object provided, return empty object
        if (string.IsNullOrEmpty(parameters))
            return (commandName, "{}");

        return (commandName, parameters);
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
