using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SharpWebview.Content;

namespace Galdr;

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
    public Dictionary<string, MethodInfo> Commands { get; init; }

    /// <summary>
    /// A value indicating if a loading screen should be shown on launch.
    /// </summary>
    public bool ShowLoading { get; init; }

    /// <summary>
    /// A message to show on the loading page.
    /// </summary>
    public string LoadingMessage { get; init; }

    /// <summary>
    /// The background color to use on the loading page.
    /// </summary>
    public string LoadingBackground { get; init; }

    /// <summary>
    /// Languages to enable spell checking for (ex. en_US).
    /// </summary>
    public List<string> SpellCheckingLanguages { get; init; }
}
