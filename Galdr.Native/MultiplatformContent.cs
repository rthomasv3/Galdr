using System;
using System.Net.Sockets;

namespace Galdr.Native;

/// <summary>
/// Platform-aware content provider — add it once and it behaves correctly on every
/// platform in both build configurations, replacing the classic <c>#if DEBUG</c>
/// content split in Program.cs.
/// </summary>
/// <remarks>
/// <para>
/// <b>Release builds</b> serve the bundled frontend: the <c>wwwroot</c> folder beside
/// the executable on desktop, bundled assets on Android, and the <c>galdr://app</c>
/// scheme on iOS.
/// </para>
/// <para>
/// <b>Debug builds</b> use the dev server instead. Debug-ness is decided at build
/// time: the Galdr.Native build targets stage a <c>galdr.dev.json</c> marker into the
/// app only when the build configuration is Debug (opt out with the
/// <c>GaldrDevMarker</c> MSBuild property), and the marker carries the dev machine's
/// LAN address so phones can find the server with zero configuration. The address is
/// a flat per-platform mapping: <c>127.0.0.1</c> on desktop, <c>10.0.2.2</c> on the
/// Android emulator, and the stamped LAN address on Android devices and everything
/// iOS. An explicit <c>devServerUrl</c> overrides the mapping and is used as-is. If
/// the marker is absent, or the mapped dev server isn't running (a single quick
/// reachability check), the bundled frontend is served, so the app always starts.
/// </para>
/// <para>
/// Note that loading a cleartext <c>http://</c> dev server requires the platform's
/// debug-time opt-in (Android <c>usesCleartextTraffic</c>, iOS ATS exception) — the
/// mobile template carries these scoped to Debug.
/// </para>
/// </remarks>
public sealed partial class MultiplatformContent : IWebviewContent
{
    #region Fields

    private const int ProbeTimeoutMs = 350;

    private readonly int _devServerPort;
    private readonly string _devServerUrl;

    private string _resolvedUrl;
    private string _unreachableDevServerUrl;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new instance of the <see cref="MultiplatformContent"/> class.
    /// </summary>
    /// <param name="devServerPort">
    /// The port the front-end dev server listens on in Debug builds (e.g. Vite's 5173).
    /// </param>
    /// <param name="devServerUrl">
    /// Optional explicit dev server URL. When set it is used as-is in Debug builds —
    /// no derivation, no reachability check. Useful when the platform mapping isn't
    /// right (multiple NICs, VPNs, `adb reverse`).
    /// </param>
    /// <param name="hostname">
    /// The virtual hostname the bundled frontend is served from on Windows desktop.
    /// Browser storage (localStorage, IndexedDB) is keyed by origin, so apps shipping
    /// an update to existing users should keep whatever hostname they used before.
    /// The mobile shells have fixed origins and ignore this.
    /// </param>
    public MultiplatformContent(int devServerPort = 5173, string devServerUrl = null,
        string hostname = "galdr.localhost")
    {
        _devServerPort = devServerPort;
        _devServerUrl = devServerUrl;
#if !ANDROID && !IOS
        _hostname = hostname;
#endif
    }

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public string ToWebviewUrl()
    {
        _resolvedUrl ??= Resolve();
        return _resolvedUrl;
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// When a Debug build fell back to bundled content because the dev server was
    /// unreachable, returns a script that shows a dismissible in-page notice (and
    /// logs a console warning) so the fallback is never mistaken for broken
    /// hot-reload. Returns <c>null</c> when no fallback happened.
    /// </summary>
    internal string GetDevServerNoticeScript()
    {
        string script = null;

        if (_unreachableDevServerUrl != null)
        {
            string message = $"Galdr: dev server not reachable at {_unreachableDevServerUrl} - showing the bundled frontend.";

            script = $$"""
                (() => {
                    console.warn('{{message}}');
                    const show = () => {
                        if (document.getElementById('galdr-dev-notice')) { return; }
                        const banner = document.createElement('div');
                        banner.id = 'galdr-dev-notice';
                        banner.style.cssText = 'position:fixed;left:0;right:0;bottom:0;z-index:2147483647;' +
                            'background:#1f2430;color:#fff;font:12px/1.4 system-ui,sans-serif;' +
                            'padding:8px 40px 8px 12px;padding-bottom:calc(8px + env(safe-area-inset-bottom));' +
                            'opacity:0.95;box-sizing:border-box;';
                        banner.textContent = '{{message}}';
                        const close = document.createElement('span');
                        close.textContent = '×';
                        close.style.cssText = 'position:absolute;right:14px;top:6px;cursor:pointer;font-size:16px;';
                        close.onclick = () => banner.remove();
                        banner.appendChild(close);
                        document.body.appendChild(banner);
                    };
                    if (document.readyState === 'loading') {
                        document.addEventListener('DOMContentLoaded', show);
                    } else {
                        show();
                    }
                })();
                """;
        }

        return script;
    }

    #endregion

    #region Private Methods

    private string Resolve()
    {
        string result = null;
        bool markerPresent = GaldrDevMarker.TryRead(out string hostIp);

        if (markerPresent)
        {
            if (!String.IsNullOrEmpty(_devServerUrl))
            {
                // Explicit means explicit — no probe, no fallback.
                result = _devServerUrl;
            }
            else
            {
                string[] candidates = GetDevServerCandidates(hostIp);

                // The reachability check exists only so the app still starts
                // (on bundled content) when the dev server isn't running yet.
                foreach (string candidate in candidates)
                {
                    if (IsReachable(candidate))
                    {
                        result = candidate;
                        break;
                    }
                }

                if (result == null)
                {
                    // Remembered so the shells can show the debug-only fallback
                    // banner — otherwise a forgotten dev server just looks like
                    // hot-reload silently not working.
                    _unreachableDevServerUrl = candidates[0];
                }
            }
        }

        return result ?? GetBundledUrl();
    }

    /// <summary>
    /// The dev server addresses to try for this platform context, in order. The
    /// marker's stamped host address is the machine that ran the build — which is also
    /// the machine running the dev server in every supported setup, including a
    /// paired-Mac iOS workflow where the build (and stamp) happen on the Windows side.
    /// </summary>
    private string[] GetDevServerCandidates(string hostIp)
    {
#if ANDROID
        // Emulator: the built-in 10.0.2.2 alias for the host machine's loopback.
        // Physical device: the stamped LAN address over wifi — zero configuration,
        // same route iOS uses. (Prefer `adb reverse`? Pass devServerUrl explicitly.)
        string[] candidates = IsAndroidEmulator()
            ? [$"http://10.0.2.2:{_devServerPort}"]
            : [$"http://{hostIp ?? "127.0.0.1"}:{_devServerPort}"];
#elif IOS
        // Simulator or device, local or paired Mac — the stamped LAN address reaches
        // the dev machine in all four cases. The loopback fallback only matters for a
        // same-Mac simulator when the build machine had no network to stamp.
        string[] candidates = [$"http://{hostIp ?? "127.0.0.1"}:{_devServerPort}"];
#else
        // Same machine, but "localhost" may mean either loopback: Node 17+ no longer
        // prefers IPv4, so a default Vite config ("localhost" binding) can end up
        // listening on ::1 only. Probing the literals covers both without paying the
        // ~2s dual-stack resolution delay that navigating to "localhost" hits on
        // Windows.
        string[] candidates =
        [
            $"http://127.0.0.1:{_devServerPort}",
            $"http://[::1]:{_devServerPort}",
        ];
#endif

        return candidates;
    }

    private static bool IsReachable(string url)
    {
        bool reachable = false;

        try
        {
            Uri uri = new Uri(url);

            // DnsSafeHost strips the brackets from IPv6 literals ("[::1]" -> "::1");
            // the parameterless Socket constructor gives a dual-stack socket, so the
            // same code path connects to either loopback.
            using Socket socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            System.Threading.Tasks.Task connect = socket.ConnectAsync(uri.DnsSafeHost, uri.Port);
            reachable = connect.Wait(ProbeTimeoutMs) && socket.Connected;
        }
        catch
        {
            // Unresolvable host, refused connection, bad URL — all mean "not this one".
        }

        return reachable;
    }

#if ANDROID
    private static string GetBundledUrl()
    {
        return $"https://{GaldrActivity.VirtualHost}/";
    }

    private static bool IsAndroidEmulator()
    {
        string fingerprint = Android.OS.Build.Fingerprint ?? "";
        string product = Android.OS.Build.Product ?? "";
        string hardware = Android.OS.Build.Hardware ?? "";

        return fingerprint.Contains("generic", StringComparison.OrdinalIgnoreCase) ||
            fingerprint.Contains("emulator", StringComparison.OrdinalIgnoreCase) ||
            product.Contains("sdk", StringComparison.OrdinalIgnoreCase) ||
            hardware.Contains("goldfish", StringComparison.OrdinalIgnoreCase) ||
            hardware.Contains("ranchu", StringComparison.OrdinalIgnoreCase);
    }
#elif IOS
    private static string GetBundledUrl()
    {
        return "galdr://app/";
    }
#endif

    #endregion
}
