namespace Galdr.Native;

/// <summary>
/// Content provider that navigates the webview to a specified URL.
/// </summary>
public sealed class UrlContent : IWebviewContent
{
    private readonly string _url;

    /// <summary>
    /// Creates a new instance of the <see cref="UrlContent"/> class.
    /// </summary>
    /// <param name="url">The URL to navigate to.</param>
    public UrlContent(string url)
    {
        _url = url;
    }

    /// <inheritdoc />
    public string ToWebviewUrl()
    {
        return _url;
    }
}
