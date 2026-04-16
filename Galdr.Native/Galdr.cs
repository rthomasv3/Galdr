using System;
using System.Collections.Generic;
using System.IO;
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
    private bool _closing;
    private GCHandle _beforeCloseCallbackHandle;
    private IntPtr _originalWndProc;

    #endregion

    #region Delegates

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate byte WindowShouldCloseDelegate(IntPtr self, IntPtr sel, IntPtr sender);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool GtkDeleteEventDelegate(IntPtr widget, IntPtr eventArg, IntPtr userData);

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
    /// Sets the title of the webview window. Can be called at any time to update
    /// the window title dynamically.
    /// </summary>
    public void SetTitle(string title)
    {
        _webView?.SetTitle(title);
    }

    /// <summary>
    /// Sets the size of the webview window. Use <see cref="WebviewHint.None"/> for
    /// the default size, <see cref="WebviewHint.Min"/> for the minimum size, or
    /// <see cref="WebviewHint.Max"/> for the maximum size.
    /// </summary>
    public void SetSize(int width, int height, WebviewHint hint)
    {
        _webView?.SetSize(width, height, hint);
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

    /// <summary>
    /// Stops the main loop and closes the webview window. If a <c>BeforeClose</c> handler
    /// is registered, the handler is bypassed on this call — the window closes immediately.
    /// </summary>
    public void Terminate()
    {
        _closing = true;
        _webView?.Terminate();
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

        if (_options.BeforeClose != null)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                SetupMacBeforeClose();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                SetupWindowsBeforeClose();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                SetupLinuxBeforeClose();
            }
        }

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

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            SetupMacMenuBar(_options.Title);
        }

        if (_options.SpellCheckingLanguages?.Count > 0 == true)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                SetupSpellCheckingLinux(_options.SpellCheckingLanguages);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                SetupSpellCheckingMac();
            }
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
        DialogService dialogService = new DialogService(_webView.GetWindow());

        _serviceProvider = _options.Services
            .AddSingleton(_ => new EventService(_webView))
            .AddSingleton<IEventService, EventService>()
            .AddSingleton<IDialogService>(dialogService)
            .AddSingleton(dialogService)
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

    private void SetupSpellCheckingLinux(List<string> languages)
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

    // --- BeforeClose platform hooks ---

    private void SetupMacBeforeClose()
    {
        try
        {
            IntPtr nsWindow = _webView.GetWindow();

            if (nsWindow != IntPtr.Zero)
            {
                IntPtr currentDelegate = ObjCBindings.objc_msgSend_IntPtr(nsWindow, ObjCBindings.sel_registerName("delegate"));

                IntPtr currentDelegateClass = currentDelegate != IntPtr.Zero
                    ? ObjCBindings.objc_msgSend_IntPtr(currentDelegate, ObjCBindings.sel_registerName("class"))
                    : ObjCBindings.objc_getClass("NSObject");

                IntPtr subclass = ObjCBindings.objc_allocateClassPair(currentDelegateClass, "GaldrWindowDelegate", IntPtr.Zero);

                if (subclass == IntPtr.Zero)
                {
                    subclass = ObjCBindings.objc_getClass("GaldrWindowDelegate");
                }
                else
                {
                    IntPtr protocol = ObjCBindings.objc_getProtocol("NSWindowDelegate");

                    if (protocol != IntPtr.Zero)
                    {
                        ObjCBindings.class_addProtocol(subclass, protocol);
                    }

                    WindowShouldCloseDelegate callback = (self, sel, sender) =>
                    {
                        byte shouldClose = 0;

                        if (_closing)
                        {
                            shouldClose = 1;
                        }
                        else
                        {
                            _options.BeforeClose(this);
                        }

                        return shouldClose;
                    };

                    _beforeCloseCallbackHandle = GCHandle.Alloc(callback);
                    IntPtr imp = Marshal.GetFunctionPointerForDelegate(callback);

                    ObjCBindings.class_addMethod(
                        subclass,
                        ObjCBindings.sel_registerName("windowShouldClose:"),
                        imp,
                        "c@:@");

                    if (currentDelegate != IntPtr.Zero)
                    {
                        IntPtr originalDelegateClass = ObjCBindings.objc_msgSend_IntPtr(currentDelegate, ObjCBindings.sel_registerName("class"));

                        string[] forwardedSelectors = { "windowDidResize:", "windowDidMove:", "windowDidBecomeKey:", "windowDidResignKey:" };

                        foreach (string selectorName in forwardedSelectors)
                        {
                            IntPtr selector = ObjCBindings.sel_registerName(selectorName);
                            IntPtr method = ObjCBindings.class_getInstanceMethod(originalDelegateClass, selector);

                            if (method != IntPtr.Zero)
                            {
                                IntPtr originalImp = ObjCBindings.method_getImplementation(method);
                                IntPtr encoding = ObjCBindings.method_getTypeEncoding(method);
                                string encodingStr = Marshal.PtrToStringAnsi(encoding);
                                ObjCBindings.class_addMethod(subclass, selector, originalImp, encodingStr);
                            }
                        }
                    }

                    ObjCBindings.objc_registerClassPair(subclass);
                }

                IntPtr newDelegate = ObjCBindings.objc_msgSend_IntPtr(
                    ObjCBindings.objc_msgSend_IntPtr(subclass, ObjCBindings.sel_registerName("alloc")),
                    ObjCBindings.sel_registerName("init"));

                ObjCBindings.objc_msgSend_IntPtr_IntPtr(nsWindow, ObjCBindings.sel_registerName("setDelegate:"), newDelegate);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to set up Mac BeforeClose: {ex.Message}");
        }
    }

    private void SetupWindowsBeforeClose()
    {
        try
        {
            IntPtr hwnd = _webView.GetWindow();

            if (hwnd != IntPtr.Zero)
            {
                WndProcDelegate wndProc = (hWnd, msg, wParam, lParam) =>
                {
                    IntPtr result;

                    if (msg == Win32Bindings.WM_CLOSE && !_closing)
                    {
                        _options.BeforeClose(this);
                        result = IntPtr.Zero;
                    }
                    else
                    {
                        result = Win32Bindings.CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
                    }

                    return result;
                };

                _beforeCloseCallbackHandle = GCHandle.Alloc(wndProc);
                IntPtr newWndProc = Marshal.GetFunctionPointerForDelegate(wndProc);
                _originalWndProc = Win32Bindings.SetWindowLongPtr(hwnd, Win32Bindings.GWL_WNDPROC, newWndProc);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to set up Windows BeforeClose: {ex.Message}");
        }
    }

    private void SetupLinuxBeforeClose()
    {
        try
        {
            IntPtr gtkWindow = _webView.GetWindow();

            if (gtkWindow != IntPtr.Zero)
            {
                GtkDeleteEventDelegate handler = (widget, eventArg, userData) =>
                {
                    bool handled;

                    if (_closing)
                    {
                        handled = false;
                    }
                    else
                    {
                        _options.BeforeClose(this);
                        handled = true;
                    }

                    return handled;
                };

                _beforeCloseCallbackHandle = GCHandle.Alloc(handler);
                IntPtr fnPtr = Marshal.GetFunctionPointerForDelegate(handler);
                GTK3Bindings.g_signal_connect_data(gtkWindow, "delete-event", fnPtr, IntPtr.Zero, IntPtr.Zero, 0);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to set up Linux BeforeClose: {ex.Message}");
        }
    }

    private void SetupMacMenuBar(string fallbackName)
    {
        try
        {
            string appName = Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? fallbackName ?? "";
            IntPtr nsAppClass = ObjCBindings.objc_getClass("NSApplication");
            IntPtr sharedApp = ObjCBindings.objc_msgSend_IntPtr(nsAppClass, ObjCBindings.sel_registerName("sharedApplication"));

            IntPtr menuClass = ObjCBindings.objc_getClass("NSMenu");
            IntPtr menuItemClass = ObjCBindings.objc_getClass("NSMenuItem");

            IntPtr allocSel = ObjCBindings.sel_registerName("alloc");
            IntPtr initSel = ObjCBindings.sel_registerName("init");
            IntPtr initWithTitleSel = ObjCBindings.sel_registerName("initWithTitle:action:keyEquivalent:");
            IntPtr setSubmenuSel = ObjCBindings.sel_registerName("setSubmenu:");
            IntPtr addItemSel = ObjCBindings.sel_registerName("addItem:");
            IntPtr setMainMenuSel = ObjCBindings.sel_registerName("setMainMenu:");

            // --- Main menu bar ---
            IntPtr mainMenu = ObjCBindings.objc_msgSend_IntPtr(
                ObjCBindings.objc_msgSend_IntPtr(menuClass, allocSel), initSel);

            // --- App menu (first item, uses app name) ---
            IntPtr appMenuItem = ObjCBindings.objc_msgSend_IntPtr(
                ObjCBindings.objc_msgSend_IntPtr(menuItemClass, allocSel), initSel);

            IntPtr appMenuTitle = ObjCBindings.CreateNSString(appName ?? "");
            IntPtr appMenu = ObjCBindings.objc_msgSend_IntPtr_IntPtr(
                ObjCBindings.objc_msgSend_IntPtr(menuClass, allocSel),
                ObjCBindings.sel_registerName("initWithTitle:"), appMenuTitle);
            ObjCBindings.ReleaseNSObject(appMenuTitle);

            IntPtr hideTitle = ObjCBindings.CreateNSString($"Hide {appName}");
            IntPtr hideKey = ObjCBindings.CreateNSString("h");
            IntPtr hideItem = ObjCBindings.objc_msgSend_IntPtr_IntPtr_IntPtr_IntPtr(
                ObjCBindings.objc_msgSend_IntPtr(menuItemClass, allocSel),
                initWithTitleSel, hideTitle, ObjCBindings.sel_registerName("hide:"), hideKey);
            ObjCBindings.objc_msgSend_IntPtr_IntPtr(appMenu, addItemSel, hideItem);
            ObjCBindings.ReleaseNSObject(hideTitle);
            ObjCBindings.ReleaseNSObject(hideKey);

            IntPtr quitTitle = ObjCBindings.CreateNSString($"Quit {appName}");
            IntPtr quitKey = ObjCBindings.CreateNSString("q");
            IntPtr quitItem = ObjCBindings.objc_msgSend_IntPtr_IntPtr_IntPtr_IntPtr(
                ObjCBindings.objc_msgSend_IntPtr(menuItemClass, allocSel),
                initWithTitleSel, quitTitle, ObjCBindings.sel_registerName("terminate:"), quitKey);
            ObjCBindings.objc_msgSend_IntPtr_IntPtr(appMenu, addItemSel, quitItem);
            ObjCBindings.ReleaseNSObject(quitTitle);
            ObjCBindings.ReleaseNSObject(quitKey);

            ObjCBindings.objc_msgSend_IntPtr_IntPtr(appMenuItem, setSubmenuSel, appMenu);
            ObjCBindings.objc_msgSend_IntPtr_IntPtr(mainMenu, addItemSel, appMenuItem);

            // --- Edit menu ---
            IntPtr editMenuItem = ObjCBindings.objc_msgSend_IntPtr(
                ObjCBindings.objc_msgSend_IntPtr(menuItemClass, allocSel), initSel);

            IntPtr editMenuTitle = ObjCBindings.CreateNSString("Edit");
            IntPtr editMenu = ObjCBindings.objc_msgSend_IntPtr_IntPtr(
                ObjCBindings.objc_msgSend_IntPtr(menuClass, allocSel),
                ObjCBindings.sel_registerName("initWithTitle:"), editMenuTitle);
            ObjCBindings.ReleaseNSObject(editMenuTitle);

            (string title, string action, string key)[] editItems =
            {
                ("Undo", "undo:", "z"),
                ("Redo", "redo:", "Z"),
                ("Cut", "cut:", "x"),
                ("Copy", "copy:", "c"),
                ("Paste", "paste:", "v"),
                ("Select All", "selectAll:", "a"),
            };

            foreach ((string title, string action, string key) in editItems)
            {
                IntPtr itemTitle = ObjCBindings.CreateNSString(title);
                IntPtr itemKey = ObjCBindings.CreateNSString(key);
                IntPtr item = ObjCBindings.objc_msgSend_IntPtr_IntPtr_IntPtr_IntPtr(
                    ObjCBindings.objc_msgSend_IntPtr(menuItemClass, allocSel),
                    initWithTitleSel, itemTitle, ObjCBindings.sel_registerName(action), itemKey);
                ObjCBindings.objc_msgSend_IntPtr_IntPtr(editMenu, addItemSel, item);
                ObjCBindings.ReleaseNSObject(itemTitle);
                ObjCBindings.ReleaseNSObject(itemKey);
            }

            ObjCBindings.objc_msgSend_IntPtr_IntPtr(editMenuItem, setSubmenuSel, editMenu);
            ObjCBindings.objc_msgSend_IntPtr_IntPtr(mainMenu, addItemSel, editMenuItem);

            // --- Attach to NSApplication ---
            ObjCBindings.objc_msgSend_IntPtr_IntPtr(sharedApp, setMainMenuSel, mainMenu);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to set up Mac menu bar: {ex.Message}");
        }
    }

    private void SetupSpellCheckingMac()
    {
        try
        {
            IntPtr nsUserDefaultsClass = ObjCBindings.objc_getClass("NSUserDefaults");
            IntPtr standardSel = ObjCBindings.sel_registerName("standardUserDefaults");
            IntPtr defaults = ObjCBindings.objc_msgSend_IntPtr(nsUserDefaultsClass, standardSel);

            IntPtr setBoolSel = ObjCBindings.sel_registerName("setBool:forKey:");

            IntPtr continuousKey = ObjCBindings.CreateNSString("WebContinuousSpellCheckingEnabled");
            ObjCBindings.objc_msgSend_bool_IntPtr_void(defaults, setBoolSel, true, continuousKey);
            ObjCBindings.ReleaseNSObject(continuousKey);

            IntPtr grammarKey = ObjCBindings.CreateNSString("WebGrammarCheckingEnabled");
            ObjCBindings.objc_msgSend_bool_IntPtr_void(defaults, setBoolSel, true, grammarKey);
            ObjCBindings.ReleaseNSObject(grammarKey);

            IntPtr autocorrectKey = ObjCBindings.CreateNSString("WebAutomaticSpellingCorrectionEnabled");
            ObjCBindings.objc_msgSend_bool_IntPtr_void(defaults, setBoolSel, false, autocorrectKey);
            ObjCBindings.ReleaseNSObject(autocorrectKey);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to enable Mac spell checking: {ex.Message}");
        }
    }

    #endregion
}
