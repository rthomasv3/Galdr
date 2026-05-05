namespace Galdr.Native;

/// <summary>
/// Top-left position of the window in screen coordinates. Returned as a
/// nullable from <see cref="Galdr.GetPosition"/> because the Wayland protocol
/// does not expose absolute window position to clients.
/// </summary>
public readonly struct WindowPosition
{
    /// <summary>
    /// X coordinate of the window's top-left corner, in logical pixels.
    /// </summary>
    public int X { get; init; }

    /// <summary>
    /// Y coordinate of the window's top-left corner, in logical pixels.
    /// </summary>
    public int Y { get; init; }
}
