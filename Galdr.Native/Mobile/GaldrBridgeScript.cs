namespace Galdr.Native;

/// <summary>
/// The JavaScript bootstrap injected into the mobile webview at document start.
/// Provides the same <c>galdrInvoke</c> contract the desktop webview binds natively,
/// so application frontends run unchanged on every platform.
/// </summary>
/// <remarks>
/// Transport is per-platform: Android exposes <c>GaldrBridgeNative.invoke</c> via
/// AddJavascriptInterface; iOS exposes <c>webkit.messageHandlers.galdr</c> via
/// WKScriptMessageHandler. Native resolves calls back through
/// <c>window.__galdrResolve(id, isError, resultJson)</c> on both. Errors reject with
/// the parsed <see cref="RPCMessage"/> shape (<c>{ message }</c>) to match desktop.
/// </remarks>
internal static class GaldrBridgeScript
{
    public const string Script = """
        (() => {
            if (window.galdrInvoke) { return; }

            const pending = new Map();
            let nextId = 1;

            window.__galdrResolve = (id, isError, json) => {
                const entry = pending.get(id);
                if (!entry) { return; }
                pending.delete(id);
                const value = json ? JSON.parse(json) : null;
                if (isError) {
                    entry.reject(value);
                } else {
                    entry.resolve(value);
                }
            };

            window.galdrInvoke = (command, args) => {
                return new Promise((resolve, reject) => {
                    const id = String(nextId++);
                    const argsJson = JSON.stringify(args ?? {});
                    if (window.GaldrBridgeNative) {
                        pending.set(id, { resolve, reject });
                        window.GaldrBridgeNative.invoke(id, command, argsJson);
                    } else if (window.webkit?.messageHandlers?.galdr) {
                        pending.set(id, { resolve, reject });
                        window.webkit.messageHandlers.galdr.postMessage({ id, command, argsJson });
                    } else {
                        reject({ message: 'no native bridge available' });
                    }
                });
            };

            // Android back handling: the native back press delegates here because
            // WebView.canGoBack() cannot see SPA pushState entries. Returns true when
            // the press was handled; false lets native background the app. Apps can
            // override with their own window.__handleBack.
            if (!window.__handleBack) {
                window.__handleBack = () => {
                    if (window.location.pathname !== '/') {
                        window.history.back();
                        return true;
                    }
                    return false;
                };
            }
        })();
        """;
}
