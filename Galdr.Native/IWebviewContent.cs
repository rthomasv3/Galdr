namespace Galdr.Native;

/// <summary>
/// Interface for content providers that supply a URL for the webview to navigate to.
/// </summary>
public interface IWebviewContent
{
    /// <summary>
    /// Returns the URL that the webview should navigate to.
    /// </summary>
    string ToWebviewUrl();
}
