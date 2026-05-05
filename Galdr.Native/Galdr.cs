using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
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
    private GCHandle _supportsSecureRestorableStateCallbackHandle;
    private GCHandle _windowChangedCallbackHandle;
    private GCHandle _windowStateChangedCallbackHandle;
    private IntPtr _originalWndProc;
    private UnhandledExceptionEventHandler _appDomainExceptionHandler;
    private EventHandler<UnobservedTaskExceptionEventArgs> _unobservedTaskExceptionHandler;
    private System.Threading.Timer _windowChangedDebounce;
    private bool _firstWindowChangedSuppressed;
    private bool _waylandDetected;
    private const int WindowChangedDebounceMs = 250;

    #endregion

    #region Delegates

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate byte WindowShouldCloseDelegate(IntPtr self, IntPtr sel, IntPtr sender);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool GtkDeleteEventDelegate(IntPtr widget, IntPtr eventArg, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate byte SupportsSecureRestorableStateDelegate(IntPtr self, IntPtr sel, IntPtr app);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void NSNotificationDelegate(IntPtr self, IntPtr sel, IntPtr notification);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool GtkConfigureEventDelegate(IntPtr widget, IntPtr eventArg, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool GtkWindowStateEventDelegate(IntPtr widget, IntPtr eventArg, IntPtr userData);

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
    /// Moves the window so its top-left corner is at the given screen coordinates.
    /// Silently no-ops when running under Wayland — clients cannot set absolute
    /// window position by Wayland protocol design. On macOS the input is interpreted
    /// in top-left screen coordinates and converted to AppKit's bottom-left frame
    /// internally.
    /// </summary>
    public void SetPosition(int x, int y)
    {
        IntPtr handle = _webView?.GetWindow() ?? IntPtr.Zero;

        if (handle != IntPtr.Zero)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (Win32Bindings.GetWindowRect(handle, out Win32Bindings.RECT rect))
                {
                    Win32Bindings.MoveWindow(handle, x, y, rect.right - rect.left, rect.bottom - rect.top, true);
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (_waylandDetected)
                {
                    System.Diagnostics.Debug.WriteLine("SetPosition is a no-op on Wayland: clients cannot set absolute window position.");
                }
                else
                {
                    GTK3Bindings.gtk_window_move(handle, x, y);
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                ObjCBindings.NSRect current = ObjCBindings.GetNSRect(handle, ObjCBindings.sel_registerName("frame"));
                double primaryHeight = GetMacPrimaryScreenHeight();
                double bottomLeftY = primaryHeight - (y + current.size.height);

                ObjCBindings.NSRect target = new ObjCBindings.NSRect
                {
                    origin = new ObjCBindings.NSPoint { x = x, y = bottomLeftY },
                    size = current.size,
                };

                ObjCBindings.objc_msgSend_NSRect_bool_void(
                    handle,
                    ObjCBindings.sel_registerName("setFrame:display:"),
                    target,
                    true);
            }
        }
    }

    /// <summary>
    /// Sets the high-level window state (Normal / Minimized / Maximized / Fullscreen).
    /// On macOS, Maximized maps to AppKit's "zoom" since macOS has no native
    /// maximize concept, and Fullscreen maps to <c>toggleFullScreen:</c> when not
    /// already in that state. Calls that target the current state are no-ops.
    /// </summary>
    public void SetWindowState(WindowState state)
    {
        IntPtr handle = _webView?.GetWindow() ?? IntPtr.Zero;

        if (handle != IntPtr.Zero)
        {
            WindowState current = GetWindowStateInternal();

            if (current != state)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    int cmd = state switch
                    {
                        WindowState.Minimized => Win32Bindings.SW_SHOWMINIMIZED,
                        WindowState.Maximized => Win32Bindings.SW_SHOWMAXIMIZED,
                        WindowState.Fullscreen => Win32Bindings.SW_SHOWMAXIMIZED, // Win32 has no native fullscreen — best effort.
                        _ => Win32Bindings.SW_RESTORE,
                    };

                    Win32Bindings.ShowWindow(handle, cmd);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    // Order matters: leave the previous state cleanly before entering the new one.
                    if (current == WindowState.Minimized)
                    {
                        GTK3Bindings.gtk_window_deiconify(handle);
                    }
                    else if (current == WindowState.Maximized)
                    {
                        GTK3Bindings.gtk_window_unmaximize(handle);
                    }
                    else if (current == WindowState.Fullscreen)
                    {
                        GTK3Bindings.gtk_window_unfullscreen(handle);
                    }

                    if (state == WindowState.Minimized)
                    {
                        GTK3Bindings.gtk_window_iconify(handle);
                    }
                    else if (state == WindowState.Maximized)
                    {
                        GTK3Bindings.gtk_window_maximize(handle);
                    }
                    else if (state == WindowState.Fullscreen)
                    {
                        GTK3Bindings.gtk_window_fullscreen(handle);
                    }
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    // Mac transitions are direction-aware: deminiaturize first if minimized,
                    // exit fullscreen via toggleFullScreen: if leaving fullscreen, unzoom via
                    // zoom: if leaving zoomed.
                    if (current == WindowState.Minimized)
                    {
                        ObjCBindings.objc_msgSend_IntPtr_IntPtr(
                            handle, ObjCBindings.sel_registerName("deminiaturize:"), IntPtr.Zero);
                    }
                    else if (current == WindowState.Fullscreen)
                    {
                        ObjCBindings.objc_msgSend_IntPtr_IntPtr(
                            handle, ObjCBindings.sel_registerName("toggleFullScreen:"), IntPtr.Zero);
                    }
                    else if (current == WindowState.Maximized)
                    {
                        ObjCBindings.objc_msgSend_IntPtr_IntPtr(
                            handle, ObjCBindings.sel_registerName("zoom:"), IntPtr.Zero);
                    }

                    if (state == WindowState.Minimized)
                    {
                        ObjCBindings.objc_msgSend_IntPtr_IntPtr(
                            handle, ObjCBindings.sel_registerName("miniaturize:"), IntPtr.Zero);
                    }
                    else if (state == WindowState.Maximized)
                    {
                        ObjCBindings.objc_msgSend_IntPtr_IntPtr(
                            handle, ObjCBindings.sel_registerName("zoom:"), IntPtr.Zero);
                    }
                    else if (state == WindowState.Fullscreen)
                    {
                        ObjCBindings.objc_msgSend_IntPtr_IntPtr(
                            handle, ObjCBindings.sel_registerName("toggleFullScreen:"), IntPtr.Zero);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Returns the current outer size of the window in logical pixels. When the
    /// window is maximized or fullscreen, this returns the current outer size,
    /// not the underlying restored size.
    /// </summary>
    public WindowSize GetSize()
    {
        (int width, int height) size = GetSizeInternal();
        return new WindowSize { Width = size.width, Height = size.height };
    }

    /// <summary>
    /// Returns the top-left position of the window in screen coordinates, or
    /// <c>null</c> when running under Wayland (clients cannot read absolute
    /// window position under the Wayland protocol).
    /// </summary>
    public WindowPosition? GetPosition()
    {
        (int x, int y)? position = GetPositionInternal();
        WindowPosition? result = null;

        if (position.HasValue)
        {
            result = new WindowPosition { X = position.Value.x, Y = position.Value.y };
        }

        return result;
    }

    /// <summary>
    /// Returns the current high-level window state. Maps to the platform's notion
    /// of minimized / maximized / fullscreen as documented on <see cref="WindowState"/>.
    /// On Linux under Wayland, <see cref="WindowState.Minimized"/> cannot be reliably
    /// detected and a minimized window will report as <see cref="WindowState.Normal"/>;
    /// see the remarks on <see cref="WindowState.Minimized"/> for the protocol-level reason.
    /// </summary>
    public WindowState GetWindowState()
    {
        return GetWindowStateInternal();
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
        UnsubscribeUnhandledExceptionHooks();

        if (_windowChangedDebounce != null)
        {
            _windowChangedDebounce.Dispose();
            _windowChangedDebounce = null;
        }

        if (_windowChangedCallbackHandle.IsAllocated)
        {
            _windowChangedCallbackHandle.Free();
        }

        if (_windowStateChangedCallbackHandle.IsAllocated)
        {
            _windowStateChangedCallbackHandle.Free();
        }

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

    private void SubscribeUnhandledExceptionHooks()
    {
        if (_options.OnUnhandledException != null)
        {
            _appDomainExceptionHandler = HandleAppDomainException;
            _unobservedTaskExceptionHandler = HandleUnobservedTaskException;

            AppDomain.CurrentDomain.UnhandledException += _appDomainExceptionHandler;
            TaskScheduler.UnobservedTaskException += _unobservedTaskExceptionHandler;
        }
    }

    private void UnsubscribeUnhandledExceptionHooks()
    {
        if (_appDomainExceptionHandler != null)
        {
            AppDomain.CurrentDomain.UnhandledException -= _appDomainExceptionHandler;
            _appDomainExceptionHandler = null;
        }

        if (_unobservedTaskExceptionHandler != null)
        {
            TaskScheduler.UnobservedTaskException -= _unobservedTaskExceptionHandler;
            _unobservedTaskExceptionHandler = null;
        }
    }

    private void HandleAppDomainException(object sender, UnhandledExceptionEventArgs e)
    {
        Exception ex = e.ExceptionObject as Exception;

        InvokeUnhandledExceptionHook(new UnhandledExceptionContext
        {
            Exception = ex,
            IsTerminating = e.IsTerminating,
            Source = UnhandledExceptionSource.AppDomain,
        });
    }

    private void HandleUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        InvokeUnhandledExceptionHook(new UnhandledExceptionContext
        {
            Exception = e.Exception,
            IsTerminating = false,
            Source = UnhandledExceptionSource.TaskScheduler,
        });

        // Mark as observed so the runtime doesn't escalate further (the CLR's default
        // policy in .NET Core and later is already non-terminating, but this keeps the
        // behavior explicit and forward-compatible).
        e.SetObserved();
    }

    private void InvokeUnhandledExceptionHook(UnhandledExceptionContext context)
    {
        try
        {
            _options.OnUnhandledException(context, _serviceProvider);
        }
        catch
        {
            // Swallow — the hook must not raise new exceptions during shutdown or from
            // a secondary task scheduler event, which could cause recursive reentry.
        }
    }

    private void RunPrimary()
    {
        SubscribeUnhandledExceptionHooks();

        _options.BeforeStartup?.Invoke();

        ConstructWebview();
        BuildServiceProvider();

        bool needsBeforeClose = _options.BeforeClose != null;
        bool needsWindowChanged = _options.WindowChanged != null;

        // The Windows path needs the WndProc subclass for either hook; the Mac and Linux
        // BeforeClose paths share enough state with WindowChanged that we run them under
        // the same trigger so the GCHandle bookkeeping has a single ownership site.
        if (needsBeforeClose)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                SetupMacBeforeClose();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                SetupLinuxBeforeClose();
            }
        }

        if (needsBeforeClose || needsWindowChanged)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                SetupWindowsWindowMessages();
            }
        }

        if (needsWindowChanged)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                SetupMacWindowChanged();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                SetupLinuxWindowChanged();
            }
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            SetupMacWindowRestoration();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _waylandDetected = GTK3Bindings.IsWayland();
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
        string commandName = null;

        try
        {
            string parameters;
            (commandName, parameters) = ExtractCommandAndArguments(paramString);

            if (_commands.TryGetValue(commandName, out CommandInfo commandInfo))
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

            if (_options.OnCommandError != null)
            {
                try
                {
                    _options.OnCommandError(
                        new CommandErrorContext
                        {
                            CommandName = commandName,
                            Exception = ex,
                        },
                        _serviceProvider);
                }
                catch
                {
                    // Swallow — the error hook must not disrupt the error response to the frontend.
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

    private void SetupWindowsWindowMessages()
    {
        try
        {
            IntPtr hwnd = _webView.GetWindow();

            if (hwnd != IntPtr.Zero)
            {
                bool hasBeforeClose = _options.BeforeClose != null;
                bool hasWindowChanged = _options.WindowChanged != null;

                WndProcDelegate wndProc = (hWnd, msg, wParam, lParam) =>
                {
                    IntPtr result;

                    if (hasBeforeClose && msg == Win32Bindings.WM_CLOSE && !_closing)
                    {
                        _options.BeforeClose(this);
                        result = IntPtr.Zero;
                    }
                    else
                    {
                        result = Win32Bindings.CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);

                        if (hasWindowChanged &&
                            (msg == Win32Bindings.WM_SIZE ||
                             msg == Win32Bindings.WM_MOVE ||
                             msg == Win32Bindings.WM_EXITSIZEMOVE))
                        {
                            ScheduleWindowChangedFire();
                        }
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
            System.Diagnostics.Debug.WriteLine($"Failed to set up Windows window messages: {ex.Message}");
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

    private void SetupMacWindowRestoration()
    {
        try
        {
            // Opt in to secure coding for restorable state by adding
            // -applicationSupportsSecureRestorableState: to the existing NSApp delegate's
            // class. Silences the AppKit warning on launch and makes the app forward-compatible
            // with future macOS versions that may tighten this requirement.
            IntPtr nsAppClass = ObjCBindings.objc_getClass("NSApplication");
            IntPtr sharedApp = ObjCBindings.objc_msgSend_IntPtr(nsAppClass, ObjCBindings.sel_registerName("sharedApplication"));
            IntPtr currentDelegate = ObjCBindings.objc_msgSend_IntPtr(sharedApp, ObjCBindings.sel_registerName("delegate"));

            if (currentDelegate != IntPtr.Zero)
            {
                IntPtr delegateClass = ObjCBindings.objc_msgSend_IntPtr(currentDelegate, ObjCBindings.sel_registerName("class"));
                IntPtr nsObjectClass = ObjCBindings.objc_getClass("NSObject");

                // Never pollute NSObject globally — if the delegate is a bare NSObject, skip.
                if (delegateClass != nsObjectClass)
                {
                    SupportsSecureRestorableStateDelegate callback = (self, sel, app) => 1;
                    _supportsSecureRestorableStateCallbackHandle = GCHandle.Alloc(callback);
                    IntPtr imp = Marshal.GetFunctionPointerForDelegate(callback);

                    ObjCBindings.class_addMethod(
                        delegateClass,
                        ObjCBindings.sel_registerName("applicationSupportsSecureRestorableState:"),
                        imp,
                        "c@:@");
                }
            }

            // Enable automatic window frame persistence. AppKit saves the frame to NSUserDefaults
            // on every resize/move and restores it on next launch under the given name. If a saved
            // frame exists, it's applied to the window immediately upon this call.
            //
            // NOTE: if a consumer uses ShowLoading, SetSize runs asynchronously after this point
            // and will overwrite the restored frame. Set size via WebviewHint.Min only when using
            // window restoration, or skip ShowLoading.
            IntPtr nsWindow = _webView.GetWindow();

            if (nsWindow != IntPtr.Zero)
            {
                IntPtr autosaveName = ObjCBindings.CreateNSString("GaldrMainWindow");
                ObjCBindings.objc_msgSend_IntPtr_IntPtr(nsWindow, ObjCBindings.sel_registerName("setFrameAutosaveName:"), autosaveName);
                ObjCBindings.ReleaseNSObject(autosaveName);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to set up Mac window restoration: {ex.Message}");
        }
    }

    // --- WindowChanged platform hooks ---

    private void SetupMacWindowChanged()
    {
        try
        {
            IntPtr nsWindow = _webView.GetWindow();

            if (nsWindow != IntPtr.Zero)
            {
                // Build a tiny NSObject subclass with a single method that dispatches every
                // observed window notification to the same handler. NotificationCenter holds
                // the observer with an unsafe-unretained reference, so allocating one instance
                // and never freeing it is the simple, leak-free pattern here — the lifetime
                // matches the Galdr instance, which is process-singleton in practice.
                IntPtr observerClass = ObjCBindings.objc_allocateClassPair(
                    ObjCBindings.objc_getClass("NSObject"),
                    "GaldrWindowChangeObserver",
                    IntPtr.Zero);

                if (observerClass == IntPtr.Zero)
                {
                    observerClass = ObjCBindings.objc_getClass("GaldrWindowChangeObserver");
                }
                else
                {
                    NSNotificationDelegate callback = (self, sel, notification) => ScheduleWindowChangedFire();
                    _windowChangedCallbackHandle = GCHandle.Alloc(callback);
                    IntPtr imp = Marshal.GetFunctionPointerForDelegate(callback);

                    ObjCBindings.class_addMethod(
                        observerClass,
                        ObjCBindings.sel_registerName("windowChanged:"),
                        imp,
                        "v@:@");

                    ObjCBindings.objc_registerClassPair(observerClass);
                }

                IntPtr observer = ObjCBindings.objc_msgSend_IntPtr(
                    ObjCBindings.objc_msgSend_IntPtr(observerClass, ObjCBindings.sel_registerName("alloc")),
                    ObjCBindings.sel_registerName("init"));

                IntPtr center = ObjCBindings.objc_msgSend_IntPtr(
                    ObjCBindings.objc_getClass("NSNotificationCenter"),
                    ObjCBindings.sel_registerName("defaultCenter"));

                IntPtr addObserverSel = ObjCBindings.sel_registerName("addObserver:selector:name:object:");
                IntPtr selector = ObjCBindings.sel_registerName("windowChanged:");

                string[] names =
                {
                    "NSWindowDidResizeNotification",
                    "NSWindowDidMoveNotification",
                    "NSWindowDidMiniaturizeNotification",
                    "NSWindowDidDeminiaturizeNotification",
                    "NSWindowDidEnterFullScreenNotification",
                    "NSWindowDidExitFullScreenNotification",
                };

                foreach (string name in names)
                {
                    IntPtr nameStr = ObjCBindings.CreateNSString(name);
                    ObjCBindings.objc_msgSend_IntPtr_IntPtr_IntPtr_IntPtr_void(
                        center, addObserverSel, observer, selector, nameStr, nsWindow);
                    ObjCBindings.ReleaseNSObject(nameStr);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to set up Mac WindowChanged: {ex.Message}");
        }
    }

    private void SetupLinuxWindowChanged()
    {
        try
        {
            IntPtr gtkWindow = _webView.GetWindow();

            if (gtkWindow != IntPtr.Zero)
            {
                GtkConfigureEventDelegate configureHandler = (widget, eventArg, userData) =>
                {
                    ScheduleWindowChangedFire();
                    return false;
                };

                _windowChangedCallbackHandle = GCHandle.Alloc(configureHandler);
                GTK3Bindings.g_signal_connect_data(
                    gtkWindow,
                    "configure-event",
                    Marshal.GetFunctionPointerForDelegate(configureHandler),
                    IntPtr.Zero, IntPtr.Zero, 0);

                GtkWindowStateEventDelegate stateHandler = (widget, eventArg, userData) =>
                {
                    ScheduleWindowChangedFire();
                    return false;
                };

                _windowStateChangedCallbackHandle = GCHandle.Alloc(stateHandler);
                GTK3Bindings.g_signal_connect_data(
                    gtkWindow,
                    "window-state-event",
                    Marshal.GetFunctionPointerForDelegate(stateHandler),
                    IntPtr.Zero, IntPtr.Zero, 0);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to set up Linux WindowChanged: {ex.Message}");
        }
    }

    private void ScheduleWindowChangedFire()
    {
        if (_options.WindowChanged != null && !_closing)
        {
            // Lazy-init the timer on first event. Same pattern as the existing GCHandle
            // bookkeeping — we don't allocate anything until a hook is wired and an event
            // actually arrives.
            if (_windowChangedDebounce == null)
            {
                Interlocked.CompareExchange(
                    ref _windowChangedDebounce,
                    new System.Threading.Timer(_ => FireWindowChanged(), null, Timeout.Infinite, Timeout.Infinite),
                    null);
            }

            _windowChangedDebounce.Change(WindowChangedDebounceMs, Timeout.Infinite);
        }
    }

    private void FireWindowChanged()
    {
        if (_webView != null && !_closing && _options.WindowChanged != null)
        {
            try
            {
                _webView.Dispatch(() =>
                {
                    if (!_closing)
                    {
                        if (!_firstWindowChangedSuppressed)
                        {
                            // Drop the first debounced fire. The framework's own initialization
                            // produces native events on every platform — initial show/configure
                            // on GTK, first setFrame: on AppKit, first WM_SIZE/WM_MOVE on Win32 —
                            // and the debounce collapses them into one fire that we silently eat.
                            // Subsequent fires are real user-driven changes.
                            _firstWindowChangedSuppressed = true;
                        }
                        else
                        {
                            try
                            {
                                WindowChangedContext context = SnapshotWindow();
                                _options.WindowChanged(this, context, _serviceProvider);
                            }
                            catch
                            {
                                // The hook must not raise — swallow to keep the debounce timer healthy.
                            }
                        }
                    }
                });
            }
            catch
            {
                // Webview may have been disposed mid-fire; ignore.
            }
        }
    }

    private WindowChangedContext SnapshotWindow()
    {
        (int width, int height) size = GetSizeInternal();
        (int x, int y)? position = GetPositionInternal();
        WindowState state = GetWindowStateInternal();

        return new WindowChangedContext
        {
            Width = size.width,
            Height = size.height,
            X = position?.x,
            Y = position?.y,
            State = state,
        };
    }

    // --- Per-platform geometry / state queries ---

    private (int width, int height) GetSizeInternal()
    {
        IntPtr handle = _webView?.GetWindow() ?? IntPtr.Zero;
        (int width, int height) result = (0, 0);

        if (handle != IntPtr.Zero)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (Win32Bindings.GetWindowRect(handle, out Win32Bindings.RECT rect))
                {
                    result = (rect.right - rect.left, rect.bottom - rect.top);
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                GTK3Bindings.gtk_window_get_size(handle, out int w, out int h);
                result = (w, h);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                ObjCBindings.NSRect frame = ObjCBindings.GetNSRect(handle, ObjCBindings.sel_registerName("frame"));
                result = ((int)frame.size.width, (int)frame.size.height);
            }
        }

        return result;
    }

    private (int x, int y)? GetPositionInternal()
    {
        IntPtr handle = _webView?.GetWindow() ?? IntPtr.Zero;
        (int x, int y)? result = null;

        if (handle != IntPtr.Zero)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (Win32Bindings.GetWindowRect(handle, out Win32Bindings.RECT rect))
                {
                    result = (rect.left, rect.top);
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (!_waylandDetected)
                {
                    GTK3Bindings.gtk_window_get_position(handle, out int x, out int y);
                    result = (x, y);
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                ObjCBindings.NSRect frame = ObjCBindings.GetNSRect(handle, ObjCBindings.sel_registerName("frame"));
                double primaryHeight = GetMacPrimaryScreenHeight();
                // NSWindow.frame.origin is bottom-left in the global screen coordinate space whose
                // origin is the bottom-left of the primary screen. Flip Y to top-left so the value
                // we expose matches Win32/X11 conventions.
                int topLeftY = (int)(primaryHeight - (frame.origin.y + frame.size.height));
                result = ((int)frame.origin.x, topLeftY);
            }
        }

        return result;
    }

    private WindowState GetWindowStateInternal()
    {
        IntPtr handle = _webView?.GetWindow() ?? IntPtr.Zero;
        WindowState result = WindowState.Normal;

        if (handle != IntPtr.Zero)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Win32Bindings.WINDOWPLACEMENT wp = new() { length = (uint)Marshal.SizeOf<Win32Bindings.WINDOWPLACEMENT>() };

                if (Win32Bindings.GetWindowPlacement(handle, ref wp))
                {
                    result = wp.showCmd switch
                    {
                        Win32Bindings.SW_SHOWMINIMIZED => WindowState.Minimized,
                        Win32Bindings.SW_SHOWMAXIMIZED => WindowState.Maximized,
                        _ => WindowState.Normal,
                    };
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                IntPtr gdkWindow = GTK3Bindings.gtk_widget_get_window(handle);

                if (gdkWindow != IntPtr.Zero)
                {
                    GTK3Bindings.GdkWindowState state = GTK3Bindings.gdk_window_get_state(gdkWindow);

                    // The xdg-shell protocol intentionally has no event for the compositor to
                    // tell a client it has been minimized — set_minimized is request-only. GTK3
                    // explicitly removed the iconified state change for the Wayland backend, so
                    // GDK_WINDOW_STATE_ICONIFIED is never set on Wayland, regardless of compositor.
                    // Detection works on X11 only; on Wayland a minimized window reports Normal.
                    if ((state & GTK3Bindings.GdkWindowState.Fullscreen) != 0)
                    {
                        result = WindowState.Fullscreen;
                    }
                    else if ((state & GTK3Bindings.GdkWindowState.Iconified) != 0)
                    {
                        result = WindowState.Minimized;
                    }
                    else if ((state & GTK3Bindings.GdkWindowState.Maximized) != 0)
                    {
                        result = WindowState.Maximized;
                    }
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                ulong styleMask = ObjCBindings.objc_msgSend_ulong(handle, ObjCBindings.sel_registerName("styleMask"));

                if ((styleMask & ObjCBindings.NSWindowStyleMaskFullScreen) != 0)
                {
                    result = WindowState.Fullscreen;
                }
                else if (ObjCBindings.objc_msgSend_bool(handle, ObjCBindings.sel_registerName("isMiniaturized")))
                {
                    result = WindowState.Minimized;
                }
                else if (ObjCBindings.objc_msgSend_bool(handle, ObjCBindings.sel_registerName("isZoomed")))
                {
                    result = WindowState.Maximized;
                }
            }
        }

        return result;
    }

    private double GetMacPrimaryScreenHeight()
    {
        // The "primary" screen — the one whose bottom-left is the origin of NSWindow's
        // global coordinate space — is screens[0], not [NSScreen mainScreen]. mainScreen
        // tracks the *key* screen, which can change at runtime.
        double height = 0.0;

        IntPtr screensArray = ObjCBindings.objc_msgSend_IntPtr(
            ObjCBindings.objc_getClass("NSScreen"),
            ObjCBindings.sel_registerName("screens"));

        if (screensArray != IntPtr.Zero)
        {
            IntPtr primary = ObjCBindings.objc_msgSend_IntPtr(
                screensArray,
                ObjCBindings.sel_registerName("firstObject"));

            if (primary != IntPtr.Zero)
            {
                ObjCBindings.NSRect frame = ObjCBindings.GetNSRect(primary, ObjCBindings.sel_registerName("frame"));
                height = frame.size.height;
            }
        }

        return height;
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
