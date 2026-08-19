using System;

namespace Galdr.Native;

/// <summary>
/// The surface a platform shell (the Android activity, the iOS view controller)
/// provides to the shared mobile <see cref="Galdr"/> host.
/// </summary>
internal interface IGaldrMobileShell
{
    /// <summary>
    /// Posts an action onto the platform's main/UI thread.
    /// </summary>
    void RunOnUiThread(Action action);

    /// <summary>
    /// Evaluates JavaScript in the webview. Must be called on the main/UI thread.
    /// </summary>
    void EvaluateJavascript(string script);
}
