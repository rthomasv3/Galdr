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
    internal static extern void objc_msgSend_bool_IntPtr_void(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.U1)] bool arg1, IntPtr arg2);

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
}
