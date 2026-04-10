using GaldrJson;

namespace Galdr.Native;

/// <summary>
/// Class used to dispatch events on the frontend.
/// </summary>
[GaldrJsonIgnore]
public sealed class EventService : IEventService
{
    #region Fields

    private readonly GaldrWebview _webView;

    #endregion

    #region Constructor

    internal EventService(GaldrWebview webview)
    {
        _webView = webview;
    }

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public void PublishEvent(string eventName, string args)
    {
        string js = $"window.dispatchEvent(new CustomEvent('{eventName}', {{ detail: {args} }}));";
        _webView.Dispatch(() => _webView.Evaluate(js));
    }

    #endregion
}
