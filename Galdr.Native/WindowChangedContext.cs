namespace Galdr.Native;

/// <summary>
/// Snapshot of the window's geometry and state, passed to the
/// <see cref="GaldrOptions.WindowChanged"/> hook after a debounced change.
/// </summary>
/// <remarks>
/// All values reflect the window at the moment the debounce timer fired.
/// Position is reported in top-left screen coordinates to match Windows and
/// X11 conventions; macOS bottom-left coordinates are converted internally.
/// </remarks>
public sealed class WindowChangedContext
{
    /// <summary>
    /// Window width in logical pixels. When <see cref="State"/> is
    /// <see cref="WindowState.Maximized"/> or <see cref="WindowState.Fullscreen"/>,
    /// this reflects the current outer size, not the underlying restored size.
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Window height in logical pixels. Same caveat as <see cref="Width"/>
    /// for non-Normal states.
    /// </summary>
    public int Height { get; init; }

    /// <summary>
    /// Top-left X coordinate of the window in screen space, or <c>null</c>
    /// when running on Wayland (clients cannot read absolute window position
    /// under the Wayland protocol).
    /// </summary>
    public int? X { get; init; }

    /// <summary>
    /// Top-left Y coordinate of the window in screen space, or <c>null</c>
    /// when running on Wayland.
    /// </summary>
    public int? Y { get; init; }

    /// <summary>
    /// Current high-level window state. Note that the event fires for all
    /// state transitions including <see cref="WindowState.Minimized"/>;
    /// consumers persisting this for restoration should typically ignore the
    /// minimized state. On Linux under Wayland, <see cref="WindowState.Minimized"/>
    /// is unreliable — see the remarks on that enum value for details.
    /// </summary>
    public WindowState State { get; init; }
}
