using System;
using System.Runtime.InteropServices;

namespace Galdr.Native;

/// <summary>
/// P/Invoke bindings for the NFD-Extended (nfd) native file dialog library.
/// </summary>
internal static class NfdBindings
{
    private const string LibName = "nfd";

    public const int NFD_ERROR = 0;
    public const int NFD_OKAY = 1;
    public const int NFD_CANCEL = 2;

    public const nuint NFD_INTERFACE_VERSION = 1;

    public const nuint NFD_WINDOW_HANDLE_TYPE_UNSET = 0;
    public const nuint NFD_WINDOW_HANDLE_TYPE_WINDOWS = 1;
    public const nuint NFD_WINDOW_HANDLE_TYPE_COCOA = 2;
    public const nuint NFD_WINDOW_HANDLE_TYPE_X11 = 3;
    public const nuint NFD_WINDOW_HANDLE_TYPE_WAYLAND = 4;

    [StructLayout(LayoutKind.Sequential)]
    public struct NfdU8FilterItem
    {
        public IntPtr Name;
        public IntPtr Spec;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NfdWindowHandle
    {
        public nuint Type;
        public IntPtr Handle;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NfdOpenDialogU8Args
    {
        public IntPtr FilterList;
        public uint FilterCount;
        public IntPtr DefaultPath;
        public NfdWindowHandle ParentWindow;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NfdSaveDialogU8Args
    {
        public IntPtr FilterList;
        public uint FilterCount;
        public IntPtr DefaultPath;
        public IntPtr DefaultName;
        public NfdWindowHandle ParentWindow;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NfdPickFolderU8Args
    {
        public IntPtr DefaultPath;
        public NfdWindowHandle ParentWindow;
    }

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int NFD_Init();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void NFD_Quit();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int NFD_OpenDialogU8_With_Impl(
        nuint version,
        out IntPtr outPath,
        ref NfdOpenDialogU8Args args);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int NFD_SaveDialogU8_With_Impl(
        nuint version,
        out IntPtr outPath,
        ref NfdSaveDialogU8Args args);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int NFD_PickFolderU8_With_Impl(
        nuint version,
        out IntPtr outPath,
        ref NfdPickFolderU8Args args);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int NFD_OpenDialogMultipleU8_With_Impl(
        nuint version,
        out IntPtr outPaths,
        ref NfdOpenDialogU8Args args);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int NFD_PathSet_GetCount(IntPtr pathSet, out uint count);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int NFD_PathSet_GetPathU8(IntPtr pathSet, uint index, out IntPtr outPath);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void NFD_PathSet_Free(IntPtr pathSet);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void NFD_FreePathU8(IntPtr filePath);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr NFD_GetError();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void NFD_ClearError();
}
