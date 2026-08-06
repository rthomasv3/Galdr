using Foundation;
using WebKit;

namespace Galdr.Native;

/// <summary>
/// Relays webview console output to os_log so Console.app / <c>log stream</c> can see
/// it — the iOS analog of Android's OnConsoleMessage → logcat forwarder.
/// </summary>
internal sealed class GaldrLogForwarder : NSObject, IWKScriptMessageHandler
{
    #region Fields

    // Fully qualified: net10.0-ios also ships an `OSLog` *namespace* (the OSLog
    // framework binding) that otherwise shadows this CoreFoundation class.
    private static readonly CoreFoundation.OSLog _log = new CoreFoundation.OSLog(
        subsystem: NSBundle.MainBundle.BundleIdentifier ?? "galdr",
        category: "webview");

    #endregion

    #region Constants

    public const string InjectedScript =
        """
        (() => {
            if (!window.webkit?.messageHandlers?.galdrLog) { return; }
            for (const level of ['log', 'info', 'warn', 'error']) {
                const original = console[level].bind(console);
                console[level] = (...args) => {
                    try {
                        const text = args.map(a => typeof a === 'string' ? a : JSON.stringify(a)).join(' ');
                        window.webkit.messageHandlers.galdrLog.postMessage(level + ': ' + text);
                    } catch { }
                    original(...args);
                };
            }
        })();
        """;

    #endregion

    #region Public Methods

    public void DidReceiveScriptMessage(WKUserContentController userContentController, WKScriptMessage message)
    {
        string line = message.Body?.ToString() ?? "";
        _log.Log(CoreFoundation.OSLogLevel.Default, $"[galdrjs] {line}");
    }

    #endregion
}
