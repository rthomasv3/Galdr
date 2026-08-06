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
