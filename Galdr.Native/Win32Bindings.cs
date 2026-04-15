using System;
using System.Runtime.InteropServices;

namespace Galdr.Native;

/// <summary>
/// P/Invoke bindings for the Windows user32 API.
/// </summary>
internal static class Win32Bindings
{
    private const string User32 = "user32.dll";

    internal const int SW_RESTORE = 9;

    /// <summary>
    /// Brings the thread that owns the given window into the foreground and activates it.
    /// </summary>
    [DllImport(User32)]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>
    /// Sets the window's show state (minimize, maximize, restore, etc.).
    /// </summary>
    [DllImport(User32)]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    /// <summary>
    /// Returns true if the window is minimized.
    /// </summary>
    [DllImport(User32)]
    internal static extern bool IsIconic(IntPtr hWnd);
}
