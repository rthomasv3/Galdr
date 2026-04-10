namespace Galdr.Native;

/// <summary>
/// Optional interface for content providers that need to configure the webview
/// before navigation (e.g., setting up virtual host mappings for local file serving).
/// Implementors can also handle navigation themselves by returning true from
/// <see cref="HandlesNavigation"/>, in which case the default webview_navigate call is skipped.
/// </summary>
public interface IWebviewContentSetup
{
    /// <summary>
    /// Called after the webview is created but before navigation.
    /// On platforms where the content provider handles navigation itself (e.g., macOS
    /// loadFileURL:allowingReadAccessToDirectory:), this method performs the navigation
    /// and <see cref="HandlesNavigation"/> returns true.
    /// </summary>
    void Setup(GaldrWebview webview);

    /// <summary>
    /// If true, the content provider handled navigation in <see cref="Setup"/> and
    /// the default webview_navigate call should be skipped.
    /// </summary>
    bool HandlesNavigation { get; }
}
