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
            //
            // Routers in the vue-router family stamp { back, current, ... } into
            // history.state (in hash and web history modes alike), which answers
            // "is there anywhere to go back to" exactly - including back: null on a
            // redirected landing screen, where backing out should background the app
            // rather than appear dead. The pathname check is the fallback for
            // path-routed apps without that convention; it can never work for hash
            // routing, where the pathname is always '/'.
            if (!window.__handleBack) {
                window.__handleBack = () => {
                    const state = window.history.state;
                    if (state && state.back !== undefined) {
                        if (state.back !== null) {
                            window.history.back();
                            return true;
                        }
                        return false;
                    }
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
