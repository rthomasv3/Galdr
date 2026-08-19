using System;
using CoreFoundation;
using Foundation;
using UIKit;
using WebKit;

namespace Galdr.Native;

/// <summary>
/// The iOS shell: hosts the WKWebView, serves the bundled SPA through the
/// <c>galdr://</c> scheme handler, injects the bridge bootstrap, and provides the
/// UI-thread/evaluate surface the shared mobile host runs on.
/// </summary>
public sealed class GaldrViewController : UIViewController, IGaldrMobileShell
{
    #region Fields

    private readonly Galdr _galdr;

    private WKWebView _webView;

    #endregion

    #region Constructor

    internal GaldrViewController(Galdr galdr)
    {
        _galdr = galdr;
    }

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        GaldrOptions options = _galdr.Options;

        WKWebViewConfiguration configuration = new();
        configuration.SetUrlSchemeHandler(new GaldrSchemeHandler(), "galdr");

        configuration.UserContentController.AddScriptMessageHandler(new GaldrScriptMessageHandler(_galdr), "galdr");
        configuration.UserContentController.AddScriptMessageHandler(new GaldrLogForwarder(), "galdrLog");

        // The bridge bootstrap, console forwarder, inset aliases, and the app's init
        // script all run at document start, before any page code.
        AddUserScript(configuration, GaldrBridgeScript.Script);
        AddUserScript(configuration, GaldrLogForwarder.InjectedScript);
        AddUserScript(configuration, InsetScript);

        if (!String.IsNullOrEmpty(options.InitScript))
        {
            AddUserScript(configuration, options.InitScript);
        }

        string startUrl = options.ContentProvider?.ToWebviewUrl() ?? "galdr://app/";

        // Debug-only banner when MultiplatformContent fell back to bundled content
        // because the dev server was unreachable. Resolution happens in ToWebviewUrl,
        // so this must come after the startUrl line.
        string devServerNotice = (options.ContentProvider as MultiplatformContent)?.GetDevServerNoticeScript();

        if (devServerNotice != null)
        {
            AddUserScript(configuration, devServerNotice);
        }

        _webView = new WKWebView(View.Bounds, configuration)
        {
            AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight,
        };

        // CSS owns the safe areas via viewport-fit=cover; without this WKWebView
        // also auto-pads the scroll view and the insets apply twice.
        _webView.ScrollView.ContentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentBehavior.Never;

        _webView.NavigationDelegate = new GaldrNavigationDelegate(startUrl);

        // Native edge-swipe back/forward drives SPA pushState history — proven smooth
        // on hardware during the spike, so it ships enabled.
        _webView.AllowsBackForwardNavigationGestures = true;

        GaldrKeyboardFocus.AllowProgrammaticKeyboard(_webView);
        GaldrKeyboardAccessory.Attach(_webView);

        if (OperatingSystem.IsIOSVersionAtLeast(16, 4))
        {
            _webView.Inspectable = options.Debug; // Safari > Develop on a Mac can attach
        }

        View.BackgroundColor = UIColor.SystemBackground;
        View.AddSubview(_webView);

        _galdr.Shell = this;

        _webView.LoadRequest(new NSUrlRequest(new NSUrl(startUrl)));

        _galdr.OnShellAttached();
    }

    #endregion

    #region Internal Methods

    void IGaldrMobileShell.RunOnUiThread(Action action)
    {
        DispatchQueue.MainQueue.DispatchAsync(action);
    }

    void IGaldrMobileShell.EvaluateJavascript(string script)
    {
        _webView?.EvaluateJavaScript(script, null);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Aliases the frontend's inset variables to the native env() values — env()
    /// resolves lazily in custom-property values, so the same SPA CSS works on both
    /// platforms (Android feeds these from native inset listeners instead). Requires
    /// viewport-fit=cover in the page. The IME variable is pinned to 0: iOS resizes
    /// the webview for the keyboard natively.
    /// </summary>
    private const string InsetScript =
        "const s = document.documentElement.style;" +
        "s.setProperty('--galdr-inset-top', 'env(safe-area-inset-top)');" +
        "s.setProperty('--galdr-inset-bottom', 'env(safe-area-inset-bottom)');" +
        "s.setProperty('--galdr-ime', '0px');";

    private static void AddUserScript(WKWebViewConfiguration configuration, string script)
    {
        configuration.UserContentController.AddUserScript(new WKUserScript(
            new NSString(script), WKUserScriptInjectionTime.AtDocumentStart, isForMainFrameOnly: true));
    }

    #endregion
}
