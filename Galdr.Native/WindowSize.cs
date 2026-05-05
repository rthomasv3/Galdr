namespace Galdr.Native;

/// <summary>
/// Outer width and height of the window in logical pixels.
/// </summary>
public readonly struct WindowSize
{
    /// <summary>
    /// Window width in logical pixels.
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Window height in logical pixels.
    /// </summary>
    public int Height { get; init; }
}
