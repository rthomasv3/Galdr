using System;
using System.Runtime.InteropServices;

namespace Galdr.Native;

/// <summary>
/// P/Invoke bindings for WebKit2GTK 4.1 spell checking functionality.
/// </summary>
internal static class WebKit2GTKBindings
{
    private const string WebKit2Lib = "libwebkit2gtk-4.1";

    /// <summary>
    /// Gets the WebKitWebContext for a WebKitWebView.
    /// </summary>
    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr webkit_web_view_get_context(IntPtr webView);

    /// <summary>
    /// Enables or disables spell checking in the WebKit context.
    /// </summary>
    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void webkit_web_context_set_spell_checking_enabled(
        IntPtr context,
        [MarshalAs(UnmanagedType.Bool)] bool enabled);

    /// <summary>
    /// Sets the languages to use for spell checking.
    /// </summary>
    /// <param name="context">The WebKit context</param>
    /// <param name="languages">Null-terminated array of language codes (e.g., "en_US", "es_ES")</param>
    [DllImport(WebKit2Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void webkit_web_context_set_spell_checking_languages(
        IntPtr context,
        IntPtr languages);

    /// <summary>
    /// Helper to convert string array to null-terminated char** for WebKit.
    /// </summary>
    internal static IntPtr CreateNullTerminatedStringArray(string[] languages)
    {
        if (languages == null || languages.Length == 0)
            return IntPtr.Zero;

        // Allocate array of pointers (one extra for null terminator)
        IntPtr[] stringPointers = new IntPtr[languages.Length + 1];

        for (int i = 0; i < languages.Length; i++)
        {
            stringPointers[i] = Marshal.StringToHGlobalAnsi(languages[i]);
        }

        stringPointers[languages.Length] = IntPtr.Zero; // Null terminator

        // Allocate unmanaged array
        IntPtr arrayPtr = Marshal.AllocHGlobal(IntPtr.Size * stringPointers.Length);
        Marshal.Copy(stringPointers, 0, arrayPtr, stringPointers.Length);

        return arrayPtr;
    }

    /// <summary>
    /// Frees memory allocated by CreateNullTerminatedStringArray.
    /// </summary>
    internal static void FreeNullTerminatedStringArray(IntPtr arrayPtr, int count)
    {
        if (arrayPtr == IntPtr.Zero)
            return;

        // Read the pointers
        IntPtr[] stringPointers = new IntPtr[count];
        Marshal.Copy(arrayPtr, stringPointers, 0, count);

        // Free each string
        for (int i = 0; i < count; i++)
        {
            if (stringPointers[i] != IntPtr.Zero)
                Marshal.FreeHGlobal(stringPointers[i]);
        }

        // Free the array itself
        Marshal.FreeHGlobal(arrayPtr);
    }
}
