using System;
using System.Runtime.InteropServices;

namespace Galdr.Native;

/// <summary>
/// P/Invoke bindings for the Windows user32 API.
/// </summary>
internal static class Win32Bindings
{
    private const string User32 = "user32.dll";

    internal const int SW_HIDE = 0;
    internal const int SW_SHOWNORMAL = 1;
    internal const int SW_SHOWMINIMIZED = 2;
    internal const int SW_SHOWMAXIMIZED = 3;
    internal const int SW_MAXIMIZE = 3;
    internal const int SW_MINIMIZE = 6;
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

    internal const int GWL_WNDPROC = -4;
    internal const uint WM_CLOSE = 0x0010;
    internal const uint WM_SIZE = 0x0005;
    internal const uint WM_MOVE = 0x0003;
    internal const uint WM_EXITSIZEMOVE = 0x0232;

    [DllImport(User32, EntryPoint = "SetWindowLongPtrW")]
    internal static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport(User32, EntryPoint = "CallWindowProcW")]
    internal static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport(User32, EntryPoint = "DefWindowProcW")]
    internal static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// Win32 RECT — top/left/bottom/right in screen coordinates.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int x;
        public int y;
    }

    /// <summary>
    /// Holds the show state and the restored, minimized, and maximized positions of a window.
    /// Use <see cref="GetWindowPlacement"/>/<see cref="SetWindowPlacement"/> for full state
    /// round-tripping; <c>rcNormalPosition</c> gives the un-maximized rect even while the
    /// window is maximized.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct WINDOWPLACEMENT
    {
        public uint length;
        public uint flags;
        public uint showCmd;
        public POINT ptMinPosition;
        public POINT ptMaxPosition;
        public RECT rcNormalPosition;
    }

    [DllImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool MoveWindow(IntPtr hWnd, int x, int y, int width, int height, [MarshalAs(UnmanagedType.Bool)] bool bRepaint);

    [DllImport(User32)]
    internal static extern uint GetWindowLongW(IntPtr hWnd, int nIndex);

    internal const int GWL_STYLE = -16;
    internal const uint WS_MAXIMIZE = 0x01000000;
    internal const uint WS_MINIMIZE = 0x20000000;
}
