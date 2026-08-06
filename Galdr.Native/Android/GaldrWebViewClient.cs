using System;
using Android.Webkit;
using AndroidX.Core.View;
using AndroidX.WebKit;

namespace Galdr.Native;

/// <summary>
/// Serves the bundled SPA from app assets under a virtual https origin with
/// history-routing fallback (any extensionless path answers with index.html), injects
/// the bridge bootstrap, and opens external links in the system browser.
/// </summary>
internal sealed class GaldrWebViewClient : WebViewClient
{
    #region Fields

    private const string AssetRoot = "wwwroot";

    private readonly WebViewAssetLoader _assetLoader;
    private readonly string _allowedHost;
    private readonly string[] _startupScripts;

    #endregion

    #region Constructor

    public GaldrWebViewClient(WebViewAssetLoader assetLoader, string startUrl, string[] startupScripts)
    {
        _assetLoader = assetLoader;
        _startupScripts = startupScripts;
        _allowedHost = Uri.TryCreate(startUrl, UriKind.Absolute, out Uri uri) ? uri.Host : GaldrActivity.VirtualHost;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Fallback injection point for the bridge bootstrap — the primary path is
    /// AddDocumentStartJavaScript, but the scripts self-guard so evaluating them
    /// again here is safe, and old WebViews without document-start support still
    /// get the bridge.
    /// </summary>
    public override void OnPageStarted(WebView view, string url, Android.Graphics.Bitmap favicon)
    {
        base.OnPageStarted(view, url, favicon);

        foreach (string script in _startupScripts)
        {
            view?.EvaluateJavascript(script, null);
        }
    }

    public override void OnPageFinished(WebView view, string url)
    {
        base.OnPageFinished(view, url);

        // CSS inset variables live on the document, so a (re)load needs a fresh pass.
        if (view != null)
        {
            ViewCompat.RequestApplyInsets(view);
        }
    }

    /// <summary>
    /// External http/https links open in the user's default browser instead of
    /// navigating the webview — same behavior the desktop webview provides.
    /// </summary>
    public override bool ShouldOverrideUrlLoading(WebView view, IWebResourceRequest request)
    {
        Android.Net.Uri url = request?.Url;

        if (url != null &&
            request.IsForMainFrame &&
            (url.Scheme == "http" || url.Scheme == "https") &&
            !String.Equals(url.Host, _allowedHost, StringComparison.OrdinalIgnoreCase))
        {
            ExternalUrlOpener.Open(url.ToString());
            return true;
        }

        return base.ShouldOverrideUrlLoading(view, request);
    }

    public override WebResourceResponse ShouldInterceptRequest(WebView view, IWebResourceRequest request)
    {
        Android.Net.Uri url = request?.Url;
        WebResourceResponse response;

        if (url?.Host == GaldrActivity.VirtualHost)
        {
            string path = url.Path ?? "/";

            if (!path.Contains('.'))
            {
                path = "/index.html";
            }

            Android.Net.Uri rewritten = Android.Net.Uri.Parse($"https://{GaldrActivity.VirtualHost}/{AssetRoot}{path}");
            response = _assetLoader.ShouldInterceptRequest(rewritten);
        }
        else
        {
            response = base.ShouldInterceptRequest(view, request);
        }

        return response;
    }

    #endregion
}
