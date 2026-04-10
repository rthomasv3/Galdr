using System;
using System.Runtime.InteropServices;

namespace Galdr.Native;

/// <summary>
/// Window size hint for the webview.
/// </summary>
public enum WebviewHint
{
    /// <summary>Width and height are default size.</summary>
    None = 0,
    /// <summary>Width and height are minimum bounds.</summary>
    Min = 1,
    /// <summary>Width and height are maximum bounds.</summary>
    Max = 2,
    /// <summary>Window size cannot be changed by the user.</summary>
    Fixed = 3,
}

/// <summary>
/// Result type for RPC return values.
/// </summary>
public enum RPCResult
{
    /// <summary>The call succeeded.</summary>
    Success = 0,
    /// <summary>The call failed.</summary>
    Error = 1,
}

/// <summary>
/// Kind of native handle to retrieve from the webview.
/// </summary>
public enum WebviewNativeHandleKind
{
    /// <summary>The native UI widget (e.g., GtkWidget on Linux, HWND on Windows, NSView on macOS).</summary>
    UIWidget = 0,
    /// <summary>The native top-level window (e.g., GtkWindow, HWND, NSWindow).</summary>
    UIWindow = 1,
    /// <summary>The browser controller (e.g., ICoreWebView2Controller on Windows).</summary>
    BrowserController = 2,
}

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void DispatchFunction(IntPtr webview, IntPtr args);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void CallBackFunction(
    [MarshalAs(UnmanagedType.LPUTF8Str)] string id,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string req,
    IntPtr arg);

internal static class WebviewBindings
{
    private const string LibName = "webview";

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr webview_create(int debug, IntPtr window);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void webview_destroy(IntPtr webview);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void webview_run(IntPtr webview);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void webview_terminate(IntPtr webview);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void webview_set_title(IntPtr webview,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string title);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void webview_set_size(IntPtr webview, int width, int height, WebviewHint hint);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void webview_navigate(IntPtr webview,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string url);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void webview_init(IntPtr webview,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string js);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void webview_eval(IntPtr webview,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string js);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void webview_dispatch(IntPtr webview, DispatchFunction dispatchFunction, IntPtr args);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void webview_bind(IntPtr webview,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        CallBackFunction callback,
        IntPtr arg);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void webview_return(IntPtr webview,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string id,
        RPCResult result,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string resultJson);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr webview_get_window(IntPtr webview);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr webview_get_native_handle(IntPtr webview, WebviewNativeHandleKind kind);
}
