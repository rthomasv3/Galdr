using System;
using System.Runtime.InteropServices;

namespace Galdr.Native;

// WebviewHint, RPCResult, and WebviewNativeHandleKind live in WebviewTypes.cs —
// they are part of the public API surface shared with the mobile targets, which
// compile without these bindings.

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
