using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Galdr.Native;

/// <summary>
/// Managed wrapper around the native webview library. Provides methods for creating
/// and controlling a webview window, binding JavaScript callbacks, and evaluating scripts.
/// </summary>
public class GaldrWebview : IDisposable
{
    #region Fields

    private readonly IntPtr _nativeWebview;
    private readonly List<GCHandle> _pinnedDelegates;
    private bool _disposed;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new webview instance.
    /// </summary>
    /// <param name="debug">If true, enables developer tools in the webview.</param>
    /// <param name="interceptExternalLinks">If true, external http/https links open in the system browser.</param>
    public GaldrWebview(bool debug = false, bool interceptExternalLinks = true)
    {
        _pinnedDelegates = new List<GCHandle>();
        _nativeWebview = WebviewBindings.webview_create(debug ? 1 : 0, IntPtr.Zero);

        if (_nativeWebview == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create webview.");
        }

        if (interceptExternalLinks)
        {
            InterceptExternalLinks();
        }
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the raw native webview handle (webview_t pointer).
    /// </summary>
    public IntPtr NativeHandle => _nativeWebview;

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the title of the webview window.
    /// </summary>
    public GaldrWebview SetTitle(string title)
    {
        WebviewBindings.webview_set_title(_nativeWebview, title);
        return this;
    }

    /// <summary>
    /// Sets the size of the webview window.
    /// </summary>
    public GaldrWebview SetSize(int width, int height, WebviewHint hint)
    {
        WebviewBindings.webview_set_size(_nativeWebview, width, height, hint);
        return this;
    }

    /// <summary>
    /// Injects JavaScript to be executed on every new document load.
    /// </summary>
    public GaldrWebview InitScript(string js)
    {
        WebviewBindings.webview_init(_nativeWebview, js);
        return this;
    }

    /// <summary>
    /// Navigates the webview to the URL provided by the content provider.
    /// </summary>
    public GaldrWebview Navigate(IWebviewContent content)
    {
        WebviewBindings.webview_navigate(_nativeWebview, content.ToWebviewUrl());
        return this;
    }

    /// <summary>
    /// Navigates the webview to the specified URL.
    /// </summary>
    public GaldrWebview Navigate(string url)
    {
        WebviewBindings.webview_navigate(_nativeWebview, url);
        return this;
    }

    /// <summary>
    /// Binds a named JavaScript function to a C# callback. The callback receives an
    /// invocation ID and a JSON string of arguments.
    /// </summary>
    public GaldrWebview Bind(string name, Action<string, string> callback)
    {
        CallBackFunction callbackFunction = (id, req, arg) => callback(id, req);
        GCHandle handle = GCHandle.Alloc(callbackFunction);
        _pinnedDelegates.Add(handle);

        WebviewBindings.webview_bind(_nativeWebview, name, callbackFunction, IntPtr.Zero);
        return this;
    }

    /// <summary>
    /// Sends a result back to the JavaScript caller for the given invocation ID.
    /// </summary>
    public void Return(string id, RPCResult result, string resultJson)
    {
        WebviewBindings.webview_return(_nativeWebview, id, result, resultJson);
    }

    /// <summary>
    /// Evaluates arbitrary JavaScript code asynchronously. The result is ignored.
    /// </summary>
    public void Evaluate(string js)
    {
        WebviewBindings.webview_eval(_nativeWebview, js);
    }

    /// <summary>
    /// Posts an action to be executed on the main thread of the webview.
    /// </summary>
    public void Dispatch(Action action)
    {
        DispatchFunction dispatchFunction = (webview, args) => action();
        GCHandle handle = GCHandle.Alloc(dispatchFunction);
        _pinnedDelegates.Add(handle);

        WebviewBindings.webview_dispatch(_nativeWebview, dispatchFunction, IntPtr.Zero);
    }

    /// <summary>
    /// Runs the main loop of the webview. Blocks until the window is closed.
    /// </summary>
    public void Run()
    {
        WebviewBindings.webview_run(_nativeWebview);
    }

    /// <summary>
    /// Stops the main loop and closes the webview window.
    /// </summary>
    public void Terminate()
    {
        WebviewBindings.webview_terminate(_nativeWebview);
    }

    /// <summary>
    /// Gets a pointer to the native window handle (HWND on Windows, GtkWindow on Linux, NSWindow on macOS).
    /// </summary>
    public IntPtr GetWindow()
    {
        return WebviewBindings.webview_get_window(_nativeWebview);
    }

    /// <summary>
    /// Gets a platform-specific native handle from the underlying webview.
    /// </summary>
    public IntPtr GetNativeHandle(WebviewNativeHandleKind kind)
    {
        return WebviewBindings.webview_get_native_handle(_nativeWebview, kind);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion

    #region Private Methods

    private void InterceptExternalLinks()
    {
        string script = @"
            document.addEventListener('click', function(e) {
                var target = e.target;
                while (target && target.tagName !== 'A') {
                    target = target.parentElement;
                }
                if (target && target.href && (target.href.startsWith('http://') || target.href.startsWith('https://'))) {
                    e.preventDefault();
                    window.galdrInvoke(JSON.stringify(['__openExternal', { ""url"": target.href }]));
                }
            });
        ";

        WebviewBindings.webview_init(_nativeWebview, script);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                foreach (GCHandle handle in _pinnedDelegates)
                {
                    if (handle.IsAllocated)
                    {
                        handle.Free();
                    }
                }
                _pinnedDelegates.Clear();
            }

            WebviewBindings.webview_destroy(_nativeWebview);
            _disposed = true;
        }
    }

    /// <summary>
    /// Destructor that ensures unmanaged resources are released.
    /// </summary>
    ~GaldrWebview()
    {
        Dispose(false);
    }

    #endregion
}
