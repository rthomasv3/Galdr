using System.Globalization;
using Android.Views;
using Android.Webkit;
using AndroidX.Core.View;

namespace Galdr.Native;

/// <summary>
/// Edge-to-edge inset strategy: the IME inset shrinks the webview natively (classic
/// adjustResize behavior, which API 35+ edge-to-edge no longer provides for free), while
/// system bar insets are forwarded to the page as CSS variables
/// (<c>--galdr-inset-top</c> / <c>--galdr-inset-bottom</c> / <c>--galdr-ime</c>) so the
/// SPA pads its own fixed elements. A <c>galdr-insets</c> CustomEvent fires on each change.
/// </summary>
internal sealed class GaldrInsetForwarder : Java.Lang.Object, IOnApplyWindowInsetsListener
{
    #region Fields

    private readonly WebView _webView;

    #endregion

    #region Constructor

    public GaldrInsetForwarder(WebView webView)
    {
        _webView = webView;
    }

    #endregion

    #region Public Methods

    public WindowInsetsCompat OnApplyWindowInsets(View view, WindowInsetsCompat insets)
    {
        AndroidX.Core.Graphics.Insets bars = insets.GetInsets(
            WindowInsetsCompat.Type.SystemBars() | WindowInsetsCompat.Type.DisplayCutout());
        AndroidX.Core.Graphics.Insets ime = insets.GetInsets(WindowInsetsCompat.Type.Ime());

        view.SetPadding(0, 0, 0, ime.Bottom);

        float density = view.Resources.DisplayMetrics.Density;
        string top = (bars.Top / density).ToString("F2", CultureInfo.InvariantCulture);
        string bottom = ((ime.Bottom > 0 ? 0 : bars.Bottom) / density).ToString("F2", CultureInfo.InvariantCulture);
        string imeCss = (ime.Bottom / density).ToString("F2", CultureInfo.InvariantCulture);

        string script = $$"""
            (() => {
                // The first inset pass can beat the document into existence during a
                // cold start; the page-finished RequestApplyInsets re-runs this.
                if (!document.documentElement) { return; }
                const style = document.documentElement.style;
                style.setProperty('--galdr-inset-top', '{{top}}px');
                style.setProperty('--galdr-inset-bottom', '{{bottom}}px');
                style.setProperty('--galdr-ime', '{{imeCss}}px');
                window.__galdrInsets = { top: {{top}}, bottom: {{bottom}}, ime: {{imeCss}} };
                window.dispatchEvent(new CustomEvent('galdr-insets'));
            })()
            """;
        _webView.EvaluateJavascript(script, null);

        return WindowInsetsCompat.Consumed;
    }

    #endregion
}
