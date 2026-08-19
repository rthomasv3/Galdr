using System;
using System.IO;

namespace Galdr.Native;

/// <summary>
/// Desktop half of <see cref="MultiplatformContent"/>: bundled mode delegates to
/// <see cref="FolderContent"/> (wwwroot beside the executable, virtual-host/file
/// serving per OS), which needs the webview-setup pass that mobile has no analog for.
/// </summary>
public sealed partial class MultiplatformContent : IWebviewContentSetup
{
    #region Fields

    private readonly string _hostname;

    private FolderContent _folderContent;

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public bool HandlesNavigation => _folderContent?.HandlesNavigation ?? false;

    /// <inheritdoc />
    public void Setup(GaldrWebview webview)
    {
        // Resolve before navigation so bundled mode can configure the webview
        // (virtual host mapping / file access) exactly the way FolderContent does.
        _resolvedUrl ??= Resolve();

        _folderContent?.Setup(webview);

        // The Windows virtual-host mapping has no directory index, so the bundled
        // entry URL is /index.html. History-mode SPA routers have no such route —
        // normalize back to / so the root route matches, the same URL shape the
        // mobile shells give the page. (file:// bundled URLs on macOS/Linux can't
        // rewrite their path and are left alone.)
        if (_folderContent != null &&
            _resolvedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            webview.InitScript(
                "if (window.location.pathname === '/index.html') { window.history.replaceState(null, '', '/'); }");
        }

        string notice = GetDevServerNoticeScript();

        if (notice != null)
        {
            webview.InitScript(notice);
        }
    }

    #endregion

    #region Private Methods

    private string GetBundledUrl()
    {
        string folderPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");

        // Handing a nonexistent folder to the platform webview surfaces as a raw
        // native error (e.g. an HRESULT from WebView2's virtual host mapping), so
        // fail here with a message that says what to actually do about it.
        if (!Directory.Exists(folderPath))
        {
            string message = $"Galdr: bundled frontend folder not found at '{folderPath}'.";

            message += _unreachableDevServerUrl != null
                ? $" The dev server at {_unreachableDevServerUrl} was not reachable either - start it, or build the frontend into wwwroot beside the executable."
                : " Build the frontend into wwwroot beside the executable.";

            throw new InvalidOperationException(message);
        }

        _folderContent = new FolderContent(folderPath, hostname: _hostname);

        return _folderContent.ToWebviewUrl();
    }

    #endregion
}
