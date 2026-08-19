using System;
using System.Runtime.InteropServices;
using CoreFoundation;
using ObjCRuntime;
using UIKit;
using WebKit;

namespace Galdr.Native;

/// <summary>
/// Hides and restores WKWebView's built-in input accessory bar — the form-assistant
/// pill (field arrows + done) iOS floats above the keyboard. It overlaps any
/// keyboard-docked UI an app draws, and there is no public API to remove it, so the
/// frontend toggles it through the <c>__setKeyboardAccessoryVisible</c> command —
/// typically hiding it while a rich editing surface holds focus and restoring it on
/// blur.
/// </summary>
/// <remarks>
/// Hiding swaps the content view's class for a runtime-built subclass whose
/// <c>inputAccessoryView</c> returns nil (the Capacitor approach). A subclass swap is
/// used instead of replacing the method because the content view may inherit
/// <c>inputAccessoryView</c> — patching an inherited implementation would change every
/// responder in the process.
/// </remarks>
internal static unsafe class GaldrKeyboardAccessory
{
    #region Fields

    private const string ObjCLib = "/usr/lib/libobjc.dylib";

    private static WKWebView _webView;
    private static IntPtr _originalClass;
    private static IntPtr _noAccessoryClass;

    #endregion

    #region Bindings

    [DllImport(ObjCLib)]
    private static extern IntPtr objc_allocateClassPair(IntPtr superclass, string name, nint extraBytes);

    [DllImport(ObjCLib)]
    private static extern void objc_registerClassPair(IntPtr cls);

    [DllImport(ObjCLib)]
    private static extern byte class_addMethod(IntPtr cls, IntPtr selector, IntPtr implementation, string types);

    [DllImport(ObjCLib)]
    private static extern IntPtr object_getClass(IntPtr obj);

    [DllImport(ObjCLib)]
    private static extern IntPtr object_setClass(IntPtr obj, IntPtr cls);

    #endregion

    #region Internal Methods

    /// <summary>
    /// Remembers the webview the toggle operates on. The content view is looked up at
    /// toggle time, not stored — WebKit can recreate it if the web content process
    /// relaunches.
    /// </summary>
    internal static void Attach(WKWebView webView)
    {
        _webView = webView;
    }

    /// <summary>
    /// Shows or hides the accessory bar. Safe from any thread; applied on the main
    /// queue. ReloadInputViews makes a change land immediately even while the
    /// keyboard is already up.
    /// </summary>
    internal static void SetVisible(bool visible)
    {
        DispatchQueue.MainQueue.DispatchAsync(() => Apply(visible));
    }

    #endregion

    #region Private Methods

    private static void Apply(bool visible)
    {
        WKWebView webView = _webView;

        if (webView != null)
        {
            foreach (UIView view in webView.ScrollView.Subviews)
            {
                // Contains, not StartsWith: while hidden the instance reports the
                // Galdr_WKContentView subclass name.
                if (view.Class.Name.Contains("WKContent", StringComparison.Ordinal))
                {
                    IntPtr current = object_getClass(view.Handle);

                    if (visible)
                    {
                        if (_noAccessoryClass != IntPtr.Zero && current == _noAccessoryClass)
                        {
                            object_setClass(view.Handle, _originalClass);
                            view.ReloadInputViews();
                        }
                    }
                    else if (current != _noAccessoryClass)
                    {
                        if (_noAccessoryClass == IntPtr.Zero)
                        {
                            _originalClass = current;
                            _noAccessoryClass = BuildNoAccessoryClass(current);
                        }

                        if (_noAccessoryClass != IntPtr.Zero)
                        {
                            object_setClass(view.Handle, _noAccessoryClass);
                            view.ReloadInputViews();
                        }
                    }
                }
            }
        }
    }

    private static IntPtr BuildNoAccessoryClass(IntPtr superclass)
    {
        IntPtr cls = objc_allocateClassPair(superclass, "Galdr_WKContentView", 0);

        if (cls != IntPtr.Zero)
        {
            delegate* unmanaged<IntPtr, IntPtr, IntPtr> implementation = &NilInputAccessoryView;
            class_addMethod(cls, Selector.GetHandle("inputAccessoryView"), (IntPtr)implementation, "@@:");
            objc_registerClassPair(cls);
        }

        return cls;
    }

    [UnmanagedCallersOnly]
    private static IntPtr NilInputAccessoryView(IntPtr self, IntPtr cmd)
    {
        return IntPtr.Zero;
    }

    #endregion
}
