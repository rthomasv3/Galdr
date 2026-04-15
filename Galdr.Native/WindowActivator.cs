using System;
using System.Runtime.InteropServices;

namespace Galdr.Native;

/// <summary>
/// Platform-specific focus and restore for a native top-level window handle.
/// </summary>
internal static class WindowActivator
{
    public static void Activate(IntPtr windowHandle)
    {
        if (windowHandle != IntPtr.Zero)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ActivateWindows(windowHandle);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                ActivateMac(windowHandle);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                ActivateLinux(windowHandle);
            }
        }
    }

    private static void ActivateWindows(IntPtr hwnd)
    {
        if (Win32Bindings.IsIconic(hwnd))
        {
            Win32Bindings.ShowWindow(hwnd, Win32Bindings.SW_RESTORE);
        }

        Win32Bindings.SetForegroundWindow(hwnd);
    }

    private static void ActivateMac(IntPtr nsWindow)
    {
        IntPtr nsAppClass = ObjCBindings.objc_getClass("NSApplication");
        IntPtr sharedApp = ObjCBindings.objc_msgSend_IntPtr(nsAppClass, ObjCBindings.sel_registerName("sharedApplication"));

        // BOOL arg for activateIgnoringOtherApps: — ObjC BOOL is passed as an integer; 1 == YES.
        ObjCBindings.objc_msgSend_IntPtr_IntPtr(sharedApp, ObjCBindings.sel_registerName("activateIgnoringOtherApps:"), (IntPtr)1);

        // nil sender for deminiaturize: and makeKeyAndOrderFront:
        ObjCBindings.objc_msgSend_IntPtr_IntPtr(nsWindow, ObjCBindings.sel_registerName("deminiaturize:"), IntPtr.Zero);
        ObjCBindings.objc_msgSend_IntPtr_IntPtr(nsWindow, ObjCBindings.sel_registerName("makeKeyAndOrderFront:"), IntPtr.Zero);
    }

    private static void ActivateLinux(IntPtr gtkWindow)
    {
        try
        {
            GTK3Bindings.gtk_window_deiconify(gtkWindow);
            GTK3Bindings.gtk_window_present(gtkWindow);
        }
        catch (DllNotFoundException)
        {
        }
    }
}
