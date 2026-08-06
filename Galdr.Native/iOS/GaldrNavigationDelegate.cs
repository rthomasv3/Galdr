using System;
using Foundation;
using WebKit;

namespace Galdr.Native;

/// <summary>
/// Handles WKWebView content-process death and external-link policy. iOS can jetsam
/// the WebContent process of a backgrounded app independently of the app process;
/// without a handler the webview silently goes blank on resume. The policy — proven in
/// the spike — is to reload the <em>current</em> URL, which restores the SPA route
/// (through the scheme handler's deep-link fallback) with localStorage intact.
/// </summary>
internal sealed class GaldrNavigationDelegate : WKNavigationDelegate
{
    #region Fields

    // Fully qualified: net10.0-ios also ships an `OSLog` namespace that shadows this class.
    private static readonly CoreFoundation.OSLog _log = new CoreFoundation.OSLog(
        subsystem: NSBundle.MainBundle.BundleIdentifier ?? "galdr",
        category: "lifecycle");

    private readonly string _allowedHost;
    private readonly bool _allowedIsHttp;

    #endregion

    #region Constructor

    public GaldrNavigationDelegate(string startUrl)
    {
        if (Uri.TryCreate(startUrl, UriKind.Absolute, out Uri uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            _allowedHost = uri.Host;
            _allowedIsHttp = true;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// External http/https links open in the user's default browser instead of
    /// navigating the webview — same behavior the desktop webview provides.
    /// </summary>
    public override void DecidePolicy(WKWebView webView, WKNavigationAction navigationAction, Action<WKNavigationActionPolicy> decisionHandler)
    {
        NSUrl url = navigationAction.Request?.Url;
        string scheme = url?.Scheme?.ToLowerInvariant();
        bool isMainFrame = navigationAction.TargetFrame == null || navigationAction.TargetFrame.MainFrame;

        if (url != null && isMainFrame &&
            (scheme == "http" || scheme == "https") &&
            !(_allowedIsHttp && String.Equals(url.Host, _allowedHost, StringComparison.OrdinalIgnoreCase)))
        {
            ExternalUrlOpener.Open(url.ToString());
            decisionHandler(WKNavigationActionPolicy.Cancel);
        }
        else
        {
            decisionHandler(WKNavigationActionPolicy.Allow);
        }
    }

    /// <inheritdoc />
    public override void ContentProcessDidTerminate(WKWebView webView)
    {
        _log.Log(CoreFoundation.OSLogLevel.Default, "[galdr] webContentProcessDidTerminate -> Reload()");
        webView.Reload();
    }

    #endregion
}
