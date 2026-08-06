using Android.Content;
using Android.Webkit;

namespace Galdr.Native;

/// <summary>
/// Two jobs: forward console output to logcat (WebView drops console.* for
/// non-debuggable apps), and implement onShowFileChooser — without it,
/// input type="file" silently does nothing.
/// </summary>
internal sealed class GaldrWebChromeClient : WebChromeClient
{
    #region Fields

    private readonly GaldrActivity _activity;

    #endregion

    #region Constructor

    public GaldrWebChromeClient(GaldrActivity activity)
    {
        _activity = activity;
    }

    #endregion

    #region Public Methods

    public override bool OnConsoleMessage(ConsoleMessage consoleMessage)
    {
        if (consoleMessage != null)
        {
            Android.Util.Log.Info("Galdr", consoleMessage.Message() ?? string.Empty);
        }

        return true;
    }

    public override bool OnShowFileChooser(WebView webView, IValueCallback filePathCallback, FileChooserParams fileChooserParams)
    {
        _activity.PendingFilePathCallback?.OnReceiveValue(null);
        _activity.PendingFilePathCallback = filePathCallback;

        Intent intent = fileChooserParams?.CreateIntent() ?? new Intent(Intent.ActionGetContent).SetType("*/*");
        _activity.LaunchFileChooser(intent);

        return true;
    }

    #endregion
}
