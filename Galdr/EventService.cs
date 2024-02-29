using Newtonsoft.Json;
using SharpWebview;

namespace Galdr;

/// <summary>
/// Class used to dispatch events on the frontend.
/// </summary>
public sealed class EventService
{
    #region Fields

    private readonly Webview _webView;

    #endregion

    #region Constructor

    internal EventService(Webview webview)
    {
        _webView = webview;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Dispatches a new CustomEvent of the given name on the main window and thread.
    /// </summary>
    public void PublishEvent<T>(string eventName, T args)
    {
        string js = $"window.dispatchEvent(new CustomEvent('{eventName}', {{ detail: {JsonConvert.SerializeObject(args)} }}));";
        _webView.Dispatch(() => _webView.Evaluate(js));
    }

    #endregion
}
