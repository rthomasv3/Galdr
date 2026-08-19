using System;
using System.Runtime.InteropServices;
using ObjCRuntime;
using UIKit;
using WebKit;

namespace Galdr.Native;

/// <summary>
/// Lets programmatic focus raise the on-screen keyboard. WKWebView only shows the
/// keyboard when focus comes from a real tap on the element (the private
/// keyboardDisplayRequiresUserAction behavior, which has no public toggle), so SPA
/// idioms like focusing a search field after navigation land DOM focus with no
/// keyboard. The fix is the one Capacitor and react-native-webview ship: replace
/// WKContentView's focus notification so it always reports user interaction.
/// </summary>
internal static unsafe class GaldrKeyboardFocus
{
    #region Fields

    private const string ObjCLib = "/usr/lib/libobjc.dylib";

    private static IntPtr _original;

    #endregion

    #region Bindings

    [DllImport(ObjCLib)]
    private static extern IntPtr class_getInstanceMethod(IntPtr cls, IntPtr selector);

    [DllImport(ObjCLib)]
    private static extern IntPtr method_getImplementation(IntPtr method);

    [DllImport(ObjCLib)]
    private static extern IntPtr method_setImplementation(IntPtr method, IntPtr implementation);

    [DllImport(ObjCLib)]
    private static extern IntPtr object_getClass(IntPtr obj);

    #endregion

    #region Internal Methods

    /// <summary>
    /// Installs the override on the webview's content view class. Safe to call once
    /// per process; WebKit changing its private class or selector degrades to the
    /// default keyboard behavior instead of failing.
    /// </summary>
    internal static void AllowProgrammaticKeyboard(WKWebView webView)
    {
        if (_original == IntPtr.Zero)
        {
            // The content view is a private subview of the webview's scroll view;
            // matched by class-name prefix rather than exact name so WebKit renames
            // stay non-fatal.
            foreach (UIView view in webView.ScrollView.Subviews)
            {
                if (view.Class.Name.StartsWith("WKContent", StringComparison.Ordinal))
                {
                    IntPtr selector = Selector.GetHandle(
                        "_elementDidFocus:userIsInteracting:blurPreviousNode:activityStateChanges:userObject:");
                    IntPtr method = class_getInstanceMethod(object_getClass(view.Handle), selector);

                    if (method != IntPtr.Zero)
                    {
                        _original = method_getImplementation(method);
                        delegate* unmanaged<IntPtr, IntPtr, IntPtr, byte, byte, nint, IntPtr, void> replacement = &ElementDidFocus;
                        method_setImplementation(method, (IntPtr)replacement);
                    }
                }
            }
        }
    }

    #endregion

    #region Private Methods

    // Signature mirrors WebKit's -[WKContentView _elementDidFocus:userIsInteracting:
    // blurPreviousNode:activityStateChanges:userObject:]: the element info is a C++
    // reference (a pointer here), BOOLs are single bytes on every current ABI, and
    // the activity-state OptionSet is a register-passed integer.
    [UnmanagedCallersOnly]
    private static void ElementDidFocus(IntPtr self, IntPtr cmd, IntPtr information, byte userIsInteracting,
        byte blurPreviousNode, nint activityStateChanges, IntPtr userObject)
    {
        ((delegate* unmanaged<IntPtr, IntPtr, IntPtr, byte, byte, nint, IntPtr, void>)_original)(
            self, cmd, information, 1, blurPreviousNode, activityStateChanges, userObject);
    }

    #endregion
}
