using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Android.Webkit;
using Java.Interop;

namespace Galdr.Native;

/// <summary>
/// Native side of the galdrInvoke bridge. The injected bootstrap calls
/// <c>GaldrBridgeNative.invoke(id, command, argsJson)</c>; results come back
/// asynchronously via evaluateJavascript into <c>window.__galdrResolve</c>.
/// </summary>
/// <remarks>
/// The [Export]-based Java binding requires the <c>Mono.Android.Export</c> assembly at
/// runtime; the Galdr.Native buildTransitive targets root it for trimmed builds.
/// </remarks>
public sealed class GaldrJsBridge : Java.Lang.Object
{
    #region Fields

    private readonly Galdr _galdr;

    #endregion

    #region Constructor

    internal GaldrJsBridge(Galdr galdr)
    {
        _galdr = galdr;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Entry point called from JavaScript. Runs on the WebView's JavaBridge thread,
    /// so the command is dispatched to the thread pool immediately.
    /// </summary>
    [JavascriptInterface]
    [Export("invoke")]
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "The buildTransitive targets root Mono.Android.Export (RootMode=All), so the [Export] binding's dynamic support survives trimming — proven on-device with TrimMode=full.")]
    public void Invoke(string id, string command, string argsJson)
    {
        Task.Run(() => _galdr.HandleCommand(id, command, argsJson));
    }

    #endregion
}
