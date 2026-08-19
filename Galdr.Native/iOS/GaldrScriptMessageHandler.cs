using System.Threading.Tasks;
using Foundation;
using WebKit;

namespace Galdr.Native;

/// <summary>
/// Native side of the galdrInvoke bridge. The injected bootstrap posts
/// <c>{ id, command, argsJson }</c> to <c>webkit.messageHandlers.galdr</c>; results
/// come back asynchronously via JavaScript evaluation into <c>window.__galdrResolve</c>.
/// </summary>
internal sealed class GaldrScriptMessageHandler : NSObject, IWKScriptMessageHandler
{
    #region Fields

    private readonly Galdr _galdr;

    #endregion

    #region Constructor

    public GaldrScriptMessageHandler(Galdr galdr)
    {
        _galdr = galdr;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Entry point called from JavaScript. Arrives on the main thread, so the command
    /// is dispatched to the thread pool immediately.
    /// </summary>
    public void DidReceiveScriptMessage(WKUserContentController userContentController, WKScriptMessage message)
    {
        if (message.Body is NSDictionary body)
        {
            string id = body.ObjectForKey(new NSString("id"))?.ToString();
            string command = body.ObjectForKey(new NSString("command"))?.ToString();
            string argsJson = body.ObjectForKey(new NSString("argsJson"))?.ToString();

            if (id != null && command != null)
            {
                Task.Run(() => _galdr.HandleCommand(id, command, argsJson));
            }
        }
    }

    #endregion
}
