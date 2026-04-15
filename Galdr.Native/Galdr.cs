using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using GaldrJson;
using Microsoft.Extensions.DependencyInjection;

namespace Galdr.Native;

/// <summary>
/// Class used to create a <see cref="Webview"/> and handle interactions between the frontend and backend.
/// </summary>
[GaldrJsonIgnore]
public class Galdr : IDisposable
{
    #region Fields

    private readonly GaldrOptions _options;
    private readonly Dictionary<string, CommandInfo> _commands;
    private readonly IGaldrJsonSerializer _galdrJsonSerializer;
    private readonly GaldrJsonOptions _galdrJsonOptions;
    private readonly bool _debug;

    private GaldrWebview _webView;
    private IWebviewContent _mainContent;
    private IServiceProvider _serviceProvider;
    private SingleInstanceCoordinator _singleInstance;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new instance of the <see cref="Galdr"/> class.
    /// </summary>
    /// <remarks>
    /// Construction is side-effect-free — no window is created and no services are built
    /// until <see cref="Run"/> is called. This lets single-instance checks short-circuit a
    /// duplicate process before any UI or DI work happens.
    /// </remarks>
    public Galdr(GaldrOptions options)
    {
        _options = options;
        _commands = options.Commands;
        _debug = options.Debug;
        _galdrJsonSerializer = options.GaldrJsonSerializer;
        _galdrJsonOptions = options.GaldrJsonOptions;
    }

    #endregion

    #region Properties

    internal GaldrWebview Webview => _webView;

    #endregion

    #region Public Methods

    /// <summary>
    /// Runs the application. Acquires the single-instance lock (if configured), fires startup
    /// hooks, constructs the webview, and blocks on the main loop. In a duplicate process,
    /// notifies the primary and returns without creating a window.
    /// </summary>
    public Galdr Run()
    {
        if (!String.IsNullOrEmpty(_options.SingleInstanceAppId))
        {
            _singleInstance = new SingleInstanceCoordinator(_options.SingleInstanceAppId);

            if (!_singleInstance.TryAcquire())
            {
                _singleInstance.SendActivateToPrimary();
            }
            else
            {
                RunPrimary();
            }
        }
        else
        {
            RunPrimary();
        }

        return this;
    }

    /// <summary>
    /// Gets a pointer to the main window handle. Returns <see cref="IntPtr.Zero"/> before
    /// <see cref="Run"/> is called or in a duplicate process that short-circuits startup.
    /// </summary>
    public IntPtr GetWindow()
    {
        return _webView?.GetWindow() ?? IntPtr.Zero;
    }

    /// <summary>
    /// Gets a native handle of the given kind from the underlying webview. Returns
    /// <see cref="IntPtr.Zero"/> if the webview has not been constructed.
    /// </summary>
    public IntPtr GetNativeHandle(WebviewNativeHandleKind kind)
    {
        return _webView?.GetNativeHandle(kind) ?? IntPtr.Zero;
    }

    /// <summary>
    /// Evaluates arbitrary JavaScript code. Evaluation happens asynchronously, also
    /// the result of the expression is ignored. Use galdrInvoke if you want to receive
    /// notifications about the results of the evaluation.
    /// </summary>
    public void Evaluate(string javascript)
    {
        _webView?.Evaluate(javascript);
    }

    /// <summary>
    /// Posts a function to be executed on the main thread of the webview. Silently no-ops
    /// if called before <see cref="Run"/> has constructed the webview.
    /// </summary>
    public void Dispatch(Action dispatchFunc)
    {
        _webView?.Dispatch(dispatchFunc);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_singleInstance != null)
        {
            _singleInstance.Dispose();
            _singleInstance = null;
        }

        if (_mainContent is IDisposable disposableContent)
        {
            disposableContent.Dispose();
        }
        else if (_mainContent is IAsyncDisposable asyncDisposableContent)
        {
            asyncDisposableContent.DisposeAsync();
        }

        _webView?.Dispose();
    }

    #endregion

    #region Private Methods

    private void RunPrimary()
    {
        _options.BeforeStartup?.Invoke();

        ConstructWebview();
        BuildServiceProvider();

        _singleInstance?.StartListener(this, _options.SecondInstance);

        _options.Startup?.Invoke(_serviceProvider);

        if (_options.AfterStartup != null)
        {
            _webView.Dispatch(() => _options.AfterStartup(_serviceProvider));
        }

        _webView.Run();
    }

    private void ConstructWebview()
    {
        _mainContent = _options.ContentProvider ?? new LocalHostedContent(_options.Port);

        IWebviewContent loadingContent = _options.ShowLoading ?
            new LoadingContent(_options.LoadingMessage, _options.LoadingBackground) :
            _mainContent;

        _webView = new GaldrWebview(_options.Debug, true);

        if (_options.SpellCheckingLanguages?.Count > 0 == true &&
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            SetupSpellChecking(_options.SpellCheckingLanguages);
        }

        if (!String.IsNullOrEmpty(_options.InitScript))
        {
            _webView.InitScript(_options.InitScript);
        }

        bool handlesNavigation = false;

        if (_mainContent is IWebviewContentSetup setupContent)
        {
            setupContent.Setup(_webView);
            handlesNavigation = setupContent.HandlesNavigation;
        }

        _webView.SetTitle(_options.Title)
            .Bind("galdrInvoke", HandleCommand);

        if (!handlesNavigation)
        {
            _webView.Navigate(loadingContent);
        }

        if (_options.ShowLoading)
        {
            Task.Run(async () =>
            {
                await WaitForMainContentReady();

                _webView.Dispatch(async () =>
                {
                    _webView.Navigate(_mainContent);

                    await Task.Delay(250);

                    _webView.SetSize(_options.Width, _options.Height, WebviewHint.None);
                    _webView.SetSize(_options.MinWidth, _options.MinHeight, WebviewHint.Min);
                });
            });
        }
        else
        {
            _webView.SetSize(_options.Width, _options.Height, WebviewHint.None);
            _webView.SetSize(_options.MinWidth, _options.MinHeight, WebviewHint.Min);
        }
    }

    private void BuildServiceProvider()
    {
        _serviceProvider = _options.Services
            .AddSingleton(_ => new EventService(_webView))
            .AddSingleton<IEventService, EventService>()
            .AddSingleton(this)
            .BuildServiceProvider();

        if (_options.ServiceProviderAccessor != null)
        {
            _options.ServiceProviderAccessor.Provider = _serviceProvider;
        }
    }

    private async void HandleCommand(string id, string paramString)
    {
        try
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
                    if (_galdrJsonSerializer.TrySerialize(result, commandInfo.ResultType, out string json, _galdrJsonOptions))
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
        catch (Exception ex)
        {
            string json = _galdrJsonSerializer.Serialize(new RPCMessage
            {
                Message = _debug ? ex.ToString() : ex.Message
            });
            _webView.Return(id, RPCResult.Error, json);
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

    private void SetupSpellChecking(List<string> languages)
    {
        try
        {
            IntPtr window = _webView.GetWindow();
            IntPtr webkitWebView = FindWebKitWebView(window);

            if (webkitWebView != IntPtr.Zero)
            {
                // Get the WebKit context
                IntPtr context = WebKit2GTKBindings.webkit_web_view_get_context(webkitWebView);

                if (context != IntPtr.Zero)
                {
                    // Enable spell checking
                    WebKit2GTKBindings.webkit_web_context_set_spell_checking_enabled(context, true);

                    // Set languages
                    IntPtr languagesPtr = WebKit2GTKBindings.CreateNullTerminatedStringArray(languages.ToArray());

                    try
                    {
                        WebKit2GTKBindings.webkit_web_context_set_spell_checking_languages(context, languagesPtr);
                    }
                    finally
                    {
                        // Clean up allocated memory
                        WebKit2GTKBindings.FreeNullTerminatedStringArray(languagesPtr, languages.Count);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Spell checking is non-critical, so we log and continue
            System.Diagnostics.Debug.WriteLine($"Failed to enable spell checking: {ex.Message}");
        }
    }

    private IntPtr FindWebKitWebView(IntPtr widget)
    {
        if (widget == IntPtr.Zero)
            return IntPtr.Zero;

        // Get the type of this widget
        IntPtr widgetType = GTK3Bindings.G_TYPE_FROM_INSTANCE(widget);
        IntPtr typeNamePtr = GTK3Bindings.g_type_name(widgetType);

        if (typeNamePtr != IntPtr.Zero)
        {
            string typeName = Marshal.PtrToStringAnsi(typeNamePtr);

            // Check if this is the WebKitWebView
            if (typeName == "WebKitWebView")
                return widget;
        }

        // Try as a GtkBin (single child container)
        IntPtr binChild = GTK3Bindings.gtk_bin_get_child(widget);
        if (binChild != IntPtr.Zero)
        {
            IntPtr found = FindWebKitWebView(binChild);
            if (found != IntPtr.Zero)
                return found;
        }

        // Try as a GtkContainer (multi-child container)
        IntPtr children = GTK3Bindings.gtk_container_get_children(widget);
        if (children != IntPtr.Zero)
        {
            try
            {
                foreach (IntPtr child in GTK3Bindings.IterateGList(children))
                {
                    IntPtr found = FindWebKitWebView(child);
                    if (found != IntPtr.Zero)
                        return found;
                }
            }
            finally
            {
                GTK3Bindings.g_list_free(children);
            }
        }

        return IntPtr.Zero;
    }

    #endregion
}
