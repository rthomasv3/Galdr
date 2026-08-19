using System;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Webkit;
using Android.Widget;
using AndroidX.Core.View;
using AndroidX.WebKit;

namespace Galdr.Native;

/// <summary>
/// The Android entry point for a Galdr application. Android has no managed <c>Main</c> —
/// the OS instantiates a manifest-declared activity — so the app carries a small subclass
/// of this activity whose only job is to route the OS entry into the app's existing
/// <c>Main</c> via <see cref="RunMain"/>. Everything else (builder, commands, DI, hooks)
/// is the same code that runs on desktop.
/// </summary>
/// <remarks>
/// The subclass should declare an <c>[Activity]</c> attribute with <c>MainLauncher = true</c>,
/// <c>WindowSoftInputMode = SoftInput.AdjustResize</c>, and the standard webview-app
/// <c>ConfigurationChanges</c> list (orientation, screen size, density, etc.) so rotation
/// does not rebuild the webview and reboot the SPA:
/// <code>
/// [Activity(Label = "@string/app_name", MainLauncher = true,
///     Theme = "@android:style/Theme.Material.NoActionBar",
///     WindowSoftInputMode = SoftInput.AdjustResize,
///     ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize |
///         ConfigChanges.SmallestScreenSize | ConfigChanges.ScreenLayout |
///         ConfigChanges.KeyboardHidden | ConfigChanges.UiMode | ConfigChanges.Density)]
/// public class MainActivity : GaldrActivity
/// {
///     protected override void RunMain() => Program.Main([]);
/// }
/// </code>
/// </remarks>
public abstract class GaldrActivity : Activity, IGaldrMobileShell
{
    #region Fields

    internal const string VirtualHost = "appassets.androidplatform.net";
    private const int FileChooserRequestCode = 42;

    private static Galdr _galdr;
    private static GaldrActivity _current;

    private WebView _webView;

    internal IValueCallback PendingFilePathCallback;

    #endregion

    #region Protected Methods

    /// <summary>
    /// Invokes the application's <c>Main</c> method. Main builds the
    /// <see cref="GaldrBuilder"/> and calls <c>Build().Run()</c>; <c>Run()</c> hands the
    /// configured app back to this activity and returns. Called once per process — a
    /// re-created activity reattaches to the running app instead.
    /// </summary>
    protected abstract void RunMain();

    /// <inheritdoc />
    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        _current = this;

        if (_galdr == null)
        {
            RunMain();

            if (_galdr == null)
            {
                throw new InvalidOperationException(
                    "RunMain completed without starting a Galdr app — Main must call GaldrBuilder.Build().Run().");
            }
        }
        else
        {
            // The process outlived the activity (e.g. backed out and relaunched, or a
            // configuration change not covered by ConfigurationChanges). Services and
            // commands are still alive; only the webview needs rebuilding.
            Attach(_galdr);
        }
    }

    /// <inheritdoc />
    protected override void OnPause()
    {
        base.OnPause();
        _galdr?.InvokeBackground();
    }

    /// <inheritdoc />
    protected override void OnResume()
    {
        base.OnResume();
        _galdr?.InvokeResume();
    }

    /// <summary>
    /// Delegates back presses to the page: WebView.CanGoBack() is blind to SPA pushState
    /// entries, so the injected <c>window.__handleBack</c> walks the SPA history and
    /// reports whether it handled the press. At the root the app moves to the background
    /// (keeping a relaunch warm) rather than finishing.
    /// </summary>
    /// <remarks>
    /// OnBackPressed is deprecated on API 33+ in favor of OnBackInvokedDispatcher, but
    /// remains functional as long as the app does not opt into
    /// <c>android:enableOnBackInvokedCallback</c>. The trade-off is no predictive-back
    /// animation — revisit when adopting the dispatcher API.
    /// </remarks>
#pragma warning disable CA1422
    public override void OnBackPressed()
    {
        WebView webView = _webView;

        if (webView == null)
        {
            base.OnBackPressed();
            return;
        }

        webView.EvaluateJavascript(
            "(() => { try { return window.__handleBack ? (window.__handleBack() === true) : false } catch { return false } })()",
            new BackResultCallback(handled =>
            {
                if (handled != "true")
                {
                    RunOnUiThread(() => MoveTaskToBack(true));
                }
            }));
    }
#pragma warning restore CA1422

    /// <inheritdoc />
    protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        if (requestCode == FileChooserRequestCode)
        {
            Android.Net.Uri[] uris = WebChromeClient.FileChooserParams.ParseResult((int)resultCode, data);
            PendingFilePathCallback?.OnReceiveValue(uris);
            PendingFilePathCallback = null;
        }
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// Called by <see cref="Galdr.Run"/> to hand the configured app to the activity the
    /// OS created. Stores the process-wide instance and builds the webview.
    /// </summary>
    internal static void Handoff(Galdr galdr)
    {
        if (_current == null)
        {
            throw new InvalidOperationException(
                "Run() on Android must be initiated by the OS through a GaldrActivity subclass.");
        }

        _galdr = galdr;
        _current.Attach(galdr);
    }

    internal void LaunchFileChooser(Intent intent)
    {
        StartActivityForResult(intent, FileChooserRequestCode);
    }

    void IGaldrMobileShell.RunOnUiThread(Action action)
    {
        RunOnUiThread(action);
    }

    void IGaldrMobileShell.EvaluateJavascript(string script)
    {
        _webView?.EvaluateJavascript(script, null);
    }

    #endregion

    #region Private Methods

    private void Attach(Galdr galdr)
    {
        WindowCompat.SetDecorFitsSystemWindows(Window, false);

        WebView webView = new WebView(this);
        _webView = webView;
        webView.Settings.JavaScriptEnabled = true;
        webView.Settings.DomStorageEnabled = true;
        WebView.SetWebContentsDebuggingEnabled(galdr.Options.Debug);

        WebViewAssetLoader assetLoader = new WebViewAssetLoader.Builder()
            .AddPathHandler("/", new WebViewAssetLoader.AssetsPathHandler(this))
            .Build();

        string startUrl = galdr.Options.ContentProvider?.ToWebviewUrl() ?? $"https://{VirtualHost}/";

        webView.SetWebViewClient(new GaldrWebViewClient(assetLoader, startUrl, BuildStartupScripts(galdr)));
        webView.SetWebChromeClient(new GaldrWebChromeClient(this));
        webView.AddJavascriptInterface(new GaldrJsBridge(galdr), "GaldrBridgeNative");

        InjectDocumentStartScripts(webView, galdr);

        // WebView ignores its own padding for content layout, so the IME resize
        // has to happen on a wrapping container instead.
        FrameLayout container = new FrameLayout(this);
        container.AddView(webView);
        ViewCompat.SetOnApplyWindowInsetsListener(container, new GaldrInsetForwarder(webView));

        SetContentView(container);

        galdr.Shell = this;

        webView.LoadUrl(startUrl);
    }

    private static string[] BuildStartupScripts(Galdr galdr)
    {
        System.Collections.Generic.List<string> scripts = new() { GaldrBridgeScript.Script };

        if (!String.IsNullOrEmpty(galdr.Options.InitScript))
        {
            scripts.Add(galdr.Options.InitScript);
        }

        // Debug-only banner when MultiplatformContent fell back to bundled content
        // because the dev server was unreachable.
        string notice = (galdr.Options.ContentProvider as MultiplatformContent)?.GetDevServerNoticeScript();

        if (notice != null)
        {
            scripts.Add(notice);
        }

        return scripts.ToArray();
    }

    /// <summary>
    /// Injects the bridge bootstrap (and the app's init script) at document start where
    /// the WebView supports it. The bootstrap self-guards, so the OnPageStarted fallback
    /// in <see cref="GaldrWebViewClient"/> is a harmless double on modern WebViews and
    /// the only path on old ones.
    /// </summary>
    private static void InjectDocumentStartScripts(WebView webView, Galdr galdr)
    {
        if (WebViewFeature.IsFeatureSupported(WebViewFeature.DocumentStartScript))
        {
            System.Collections.Generic.List<string> allOrigins = new() { "*" };

            foreach (string script in BuildStartupScripts(galdr))
            {
                WebViewCompat.AddDocumentStartJavaScript(webView, script, allOrigins);
            }
        }
    }

    #endregion
}

/// <summary>
/// Marshals an EvaluateJavascript result back to a callback.
/// </summary>
internal sealed class BackResultCallback : Java.Lang.Object, IValueCallback
{
    private readonly Action<string> _onResult;

    public BackResultCallback(Action<string> onResult)
    {
        _onResult = onResult;
    }

    public void OnReceiveValue(Java.Lang.Object value)
    {
        _onResult(value?.ToString());
    }
}
