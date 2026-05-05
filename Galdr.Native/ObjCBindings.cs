using System;
using System.Runtime.InteropServices;

namespace Galdr.Native;

/// <summary>
/// P/Invoke bindings for the Objective-C runtime on macOS, used for calling
/// WKWebView methods like loadFileURL:allowingReadAccessToDirectory:.
/// </summary>
internal static class ObjCBindings
{
    private const string ObjCLib = "/usr/lib/libobjc.dylib";
    private const string FoundationLib = "/System/Library/Frameworks/Foundation.framework/Foundation";

    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr sel_registerName(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr objc_getClass(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string className);

    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    internal static extern IntPtr objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector);

    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    internal static extern IntPtr objc_msgSend_IntPtr_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    internal static extern void objc_msgSend_IntPtr_IntPtr_void(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool objc_msgSend_bool_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    internal static extern long objc_msgSend_long(IntPtr receiver, IntPtr selector);

    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    internal static extern ulong objc_msgSend_ulong(IntPtr receiver, IntPtr selector);

    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool objc_msgSend_bool(IntPtr receiver, IntPtr selector);

    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    internal static extern void objc_msgSend_NSRect_bool_void(IntPtr receiver, IntPtr selector, NSRect rect, [MarshalAs(UnmanagedType.U1)] bool display);

    /// <summary>
    /// Variant used on x86_64 to retrieve a "large" struct return (>16 bytes) — Apple's
    /// x86_64 ABI passes the return slot as a hidden first pointer through a separate
    /// dispatch entry point. On arm64 all struct returns flow through plain
    /// <c>objc_msgSend</c>, so this is x86_64-only.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend_stret")]
    private static extern void objc_msgSend_stret_NSRect(out NSRect ret, IntPtr receiver, IntPtr selector);

    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    private static extern NSRect objc_msgSend_NSRect_ret(IntPtr receiver, IntPtr selector);

    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    internal static extern void objc_msgSend_bool_IntPtr_void(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.U1)] bool arg1, IntPtr arg2);

    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    internal static extern IntPtr objc_msgSend_IntPtr_IntPtr_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    internal static extern IntPtr objc_msgSend_IntPtr_IntPtr_IntPtr_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2, IntPtr arg3);

    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    internal static extern void objc_msgSend_IntPtr_IntPtr_IntPtr_IntPtr_void(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2, IntPtr arg3, IntPtr arg4);

    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr objc_allocateClassPair(IntPtr superclass, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, IntPtr extraBytes);

    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void objc_registerClassPair(IntPtr cls);

    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool class_addMethod(IntPtr cls, IntPtr selector, IntPtr imp, [MarshalAs(UnmanagedType.LPUTF8Str)] string types);

    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr class_getInstanceMethod(IntPtr cls, IntPtr selector);

    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr method_getImplementation(IntPtr method);

    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr method_getTypeEncoding(IntPtr method);

    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool class_addProtocol(IntPtr cls, IntPtr protocol);

    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr objc_getProtocol([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    /// <summary>
    /// Creates an NSString from a C# string.
    /// </summary>
    internal static IntPtr CreateNSString(string str)
    {
        IntPtr nsStringClass = objc_getClass("NSString");
        IntPtr allocSel = sel_registerName("alloc");
        IntPtr initSel = sel_registerName("initWithUTF8String:");

        IntPtr allocated = objc_msgSend_IntPtr(nsStringClass, allocSel);
        IntPtr utf8Ptr = Marshal.StringToCoTaskMemUTF8(str);
        IntPtr nsString = objc_msgSend_IntPtr_IntPtr(allocated, initSel, utf8Ptr);
        Marshal.FreeCoTaskMem(utf8Ptr);

        return nsString;
    }

    /// <summary>
    /// Creates an NSURL from an NSString file path.
    /// </summary>
    internal static IntPtr CreateNSURLFromFilePath(IntPtr nsStringPath)
    {
        IntPtr nsurlClass = objc_getClass("NSURL");
        IntPtr fileUrlSel = sel_registerName("fileURLWithPath:");
        return objc_msgSend_IntPtr_IntPtr(nsurlClass, fileUrlSel, nsStringPath);
    }

    /// <summary>
    /// Releases an Objective-C object.
    /// </summary>
    internal static void ReleaseNSObject(IntPtr obj)
    {
        if (obj != IntPtr.Zero)
        {
            IntPtr releaseSel = sel_registerName("release");
            objc_msgSend_IntPtr(obj, releaseSel);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NSPoint
    {
        public double x;
        public double y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NSSize
    {
        public double width;
        public double height;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NSRect
    {
        public NSPoint origin;
        public NSSize size;
    }

    /// <summary>
    /// AppKit style-mask bit set when an NSWindow is in native fullscreen mode.
    /// Defined as <c>NSFullScreenWindowMask</c> / <c>NSWindowStyleMaskFullScreen</c>
    /// (1 &lt;&lt; 14) in the AppKit headers.
    /// </summary>
    internal const ulong NSWindowStyleMaskFullScreen = 1UL << 14;

    /// <summary>
    /// Dispatches an NSRect-returning selector across both x86_64 and arm64.
    /// On x86_64 NSRect (32 bytes) is returned via the <c>objc_msgSend_stret</c>
    /// hidden-pointer ABI; on arm64 it flows through the standard <c>objc_msgSend</c>
    /// in registers. The runtime check picks the matching entry point so the same
    /// helper works on both Mac architectures.
    /// </summary>
    internal static NSRect GetNSRect(IntPtr receiver, IntPtr selector)
    {
        NSRect rect;

        if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
        {
            objc_msgSend_stret_NSRect(out rect, receiver, selector);
        }
        else
        {
            rect = objc_msgSend_NSRect_ret(receiver, selector);
        }

        return rect;
    }
}
