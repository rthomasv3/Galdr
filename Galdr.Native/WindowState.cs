namespace Galdr.Native;

/// <summary>
/// High-level window state. The four values are mutually exclusive — a window
/// is in exactly one of these states at any time.
/// </summary>
public enum WindowState
{
    /// <summary>
    /// Standard windowed state with user-controllable size and position.
    /// </summary>
    Normal,

    /// <summary>
    /// Window is minimized to the taskbar / dock.
    /// </summary>
    /// <remarks>
    /// Detection is unreliable on Linux under Wayland: the xdg-shell protocol has no
    /// server-to-client event reporting that a surface has been minimized, and GTK3's
    /// Wayland backend never sets <c>GDK_WINDOW_STATE_ICONIFIED</c> on the toplevel.
    /// A minimized window will typically be reported as <see cref="Normal"/> on Wayland.
    /// X11, Windows, and macOS detect this state correctly.
    /// </remarks>
    Minimized,

    /// <summary>
    /// Window is maximized to fill the available work area. On macOS this
    /// corresponds to the "zoomed" state, not native fullscreen.
    /// </summary>
    Maximized,

    /// <summary>
    /// Window is in native fullscreen mode (no chrome, dedicated space on macOS).
    /// </summary>
    Fullscreen,
}
