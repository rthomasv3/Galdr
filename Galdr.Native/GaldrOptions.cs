using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace Galdr.Native;

/// <summary>
/// Class used to define the configuration for a <see cref="Galdr"/> instance.
/// </summary>
public sealed class GaldrOptions
{
    /// <summary>
    /// The title of the application window.
    /// </summary>
    public string Title { get; init; }

    /// <summary>
    /// Default for the application window width.
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Default for the application window height.
    /// </summary>
    public int Height { get; init; }

    /// <summary>
    /// Minimum bound for application window width.
    /// </summary>
    public int MinWidth { get; init; }

    /// <summary>
    /// Minimum bound for application window height.
    /// </summary>
    public int MinHeight { get; init; }

    /// <summary>
    /// Set to true to activate a debug view (if the current webview implementation supports it).
    /// </summary>
    public bool Debug { get; init; }

    /// <summary>
    /// The collection of services for use in executing commands and injecting dependencies.
    /// </summary>
    public IServiceCollection Services { get; init; }

    /// <summary>
    /// The port the content is being served from.
    /// </summary>
    public int Port { get; init; }

    /// <summary>
    /// A dictionary mapping a name for use in <c>galdrInvoke</c> with the associated C# method to call.
    /// </summary>
    public Dictionary<string, CommandInfo> Commands { get; init; }
}
