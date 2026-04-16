using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using GaldrJson;

namespace Galdr.Native;

/// <summary>
/// Class used to provide access to native system dialogs via NFD-Extended.
/// </summary>
[GaldrJsonIgnore]
public sealed class DialogService : IDialogService
{
    #region Fields

    private readonly NfdBindings.NfdWindowHandle _parentWindow;

    #endregion

    #region Constructor

    internal DialogService(IntPtr windowHandle)
    {
        int result = NfdBindings.NFD_Init();

        if (result != NfdBindings.NFD_OKAY)
        {
            IntPtr errPtr = NfdBindings.NFD_GetError();
            string message = "Unknown error";

            if (errPtr != IntPtr.Zero)
            {
                message = Marshal.PtrToStringUTF8(errPtr);
                NfdBindings.NFD_ClearError();
            }

            throw new InvalidOperationException($"NFD_Init failed: {message}");
        }

        nuint handleType = NfdBindings.NFD_WINDOW_HANDLE_TYPE_UNSET;
        IntPtr handle = IntPtr.Zero;

        if (windowHandle != IntPtr.Zero)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                handleType = NfdBindings.NFD_WINDOW_HANDLE_TYPE_WINDOWS;
                handle = windowHandle;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                handleType = NfdBindings.NFD_WINDOW_HANDLE_TYPE_COCOA;
                handle = windowHandle;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                string sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE")?.ToLower();

                if (sessionType == "x11")
                {
                    handleType = NfdBindings.NFD_WINDOW_HANDLE_TYPE_X11;
                    handle = windowHandle;
                }
            }
        }

        _parentWindow = new NfdBindings.NfdWindowHandle
        {
            Type = handleType,
            Handle = handle,
        };
    }

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public string OpenDirectoryDialog(string defaultPath = null)
    {
        List<IntPtr> allocations = new();
        string resultPath = null;

        try
        {
            NfdBindings.NfdPickFolderU8Args args = new NfdBindings.NfdPickFolderU8Args
            {
                DefaultPath = AllocUtf8OrNull(defaultPath, allocations),
                ParentWindow = _parentWindow,
            };

            int result = NfdBindings.NFD_PickFolderU8_With_Impl(
                NfdBindings.NFD_INTERFACE_VERSION, out IntPtr outPath, ref args);

            if (result == NfdBindings.NFD_OKAY)
            {
                resultPath = ExtractPath(outPath);
            }
        }
        finally
        {
            FreeAllocations(allocations);
        }

        return resultPath;
    }

    /// <inheritdoc />
    public string OpenFileDialog(string filterList = null, string defaultPath = null)
    {
        List<IntPtr> allocations = new();
        GCHandle filtersHandle = default;
        string resultPath = null;

        try
        {
            NfdBindings.NfdU8FilterItem[] filters = BuildFilters(filterList, allocations);

            if (filters != null)
            {
                filtersHandle = GCHandle.Alloc(filters, GCHandleType.Pinned);
            }

            NfdBindings.NfdOpenDialogU8Args args = new NfdBindings.NfdOpenDialogU8Args
            {
                FilterList = filtersHandle.IsAllocated ? filtersHandle.AddrOfPinnedObject() : IntPtr.Zero,
                FilterCount = filters != null ? (uint)filters.Length : 0,
                DefaultPath = AllocUtf8OrNull(defaultPath, allocations),
                ParentWindow = _parentWindow,
            };

            int result = NfdBindings.NFD_OpenDialogU8_With_Impl(
                NfdBindings.NFD_INTERFACE_VERSION, out IntPtr outPath, ref args);

            if (result == NfdBindings.NFD_OKAY)
            {
                resultPath = ExtractPath(outPath);
            }
        }
        finally
        {
            if (filtersHandle.IsAllocated) filtersHandle.Free();
            FreeAllocations(allocations);
        }

        return resultPath;
    }

    /// <inheritdoc />
    public string[] OpenFileDialogMultiple(string filterList = null, string defaultPath = null)
    {
        List<IntPtr> allocations = new();
        GCHandle filtersHandle = default;
        string[] resultPaths = null;

        try
        {
            NfdBindings.NfdU8FilterItem[] filters = BuildFilters(filterList, allocations);

            if (filters != null)
            {
                filtersHandle = GCHandle.Alloc(filters, GCHandleType.Pinned);
            }

            NfdBindings.NfdOpenDialogU8Args args = new NfdBindings.NfdOpenDialogU8Args
            {
                FilterList = filtersHandle.IsAllocated ? filtersHandle.AddrOfPinnedObject() : IntPtr.Zero,
                FilterCount = filters != null ? (uint)filters.Length : 0,
                DefaultPath = AllocUtf8OrNull(defaultPath, allocations),
                ParentWindow = _parentWindow,
            };

            int result = NfdBindings.NFD_OpenDialogMultipleU8_With_Impl(
                NfdBindings.NFD_INTERFACE_VERSION, out IntPtr outPaths, ref args);

            if (result == NfdBindings.NFD_OKAY && outPaths != IntPtr.Zero)
            {
                NfdBindings.NFD_PathSet_GetCount(outPaths, out uint count);
                List<string> paths = new();

                for (uint i = 0; i < count; i++)
                {
                    int pathResult = NfdBindings.NFD_PathSet_GetPathU8(outPaths, i, out IntPtr pathPtr);

                    if (pathResult == NfdBindings.NFD_OKAY && pathPtr != IntPtr.Zero)
                    {
                        string path = Marshal.PtrToStringUTF8(pathPtr);
                        NfdBindings.NFD_FreePathU8(pathPtr);
                        paths.Add(path);
                    }
                }

                NfdBindings.NFD_PathSet_Free(outPaths);
                resultPaths = paths.ToArray();
            }
        }
        finally
        {
            if (filtersHandle.IsAllocated) filtersHandle.Free();
            FreeAllocations(allocations);
        }

        return resultPaths;
    }

    /// <inheritdoc />
    public string OpenSaveDialog(string filterList = null, string defaultPath = null, string defaultName = null)
    {
        List<IntPtr> allocations = new();
        GCHandle filtersHandle = default;
        string resultPath = null;

        try
        {
            NfdBindings.NfdU8FilterItem[] filters = BuildFilters(filterList, allocations);

            if (filters != null)
            {
                filtersHandle = GCHandle.Alloc(filters, GCHandleType.Pinned);
            }

            NfdBindings.NfdSaveDialogU8Args args = new NfdBindings.NfdSaveDialogU8Args
            {
                FilterList = filtersHandle.IsAllocated ? filtersHandle.AddrOfPinnedObject() : IntPtr.Zero,
                FilterCount = filters != null ? (uint)filters.Length : 0,
                DefaultPath = AllocUtf8OrNull(defaultPath, allocations),
                DefaultName = AllocUtf8OrNull(defaultName, allocations),
                ParentWindow = _parentWindow,
            };

            int result = NfdBindings.NFD_SaveDialogU8_With_Impl(
                NfdBindings.NFD_INTERFACE_VERSION, out IntPtr outPath, ref args);

            if (result == NfdBindings.NFD_OKAY)
            {
                resultPath = ExtractPath(outPath);
            }
        }
        finally
        {
            if (filtersHandle.IsAllocated) filtersHandle.Free();
            FreeAllocations(allocations);
        }

        return resultPath;
    }

    #endregion

    #region Private Methods

    private string ExtractPath(IntPtr pathPtr)
    {
        string path = null;

        if (pathPtr != IntPtr.Zero)
        {
            path = Marshal.PtrToStringUTF8(pathPtr);
            NfdBindings.NFD_FreePathU8(pathPtr);
        }

        return path;
    }

    private NfdBindings.NfdU8FilterItem[] BuildFilters(string filterList, List<IntPtr> allocations)
    {
        NfdBindings.NfdU8FilterItem[] filters = null;

        if (!String.IsNullOrWhiteSpace(filterList))
        {
            IntPtr namePtr = Marshal.StringToCoTaskMemUTF8("Files");
            IntPtr specPtr = Marshal.StringToCoTaskMemUTF8(filterList);
            allocations.Add(namePtr);
            allocations.Add(specPtr);

            filters = new NfdBindings.NfdU8FilterItem[]
            {
                new NfdBindings.NfdU8FilterItem { Name = namePtr, Spec = specPtr }
            };
        }

        return filters;
    }

    private IntPtr AllocUtf8OrNull(string value, List<IntPtr> allocations)
    {
        IntPtr ptr = IntPtr.Zero;

        if (!String.IsNullOrEmpty(value))
        {
            ptr = Marshal.StringToCoTaskMemUTF8(value);
            allocations.Add(ptr);
        }

        return ptr;
    }

    private void FreeAllocations(List<IntPtr> allocations)
    {
        foreach (IntPtr ptr in allocations)
        {
            Marshal.FreeCoTaskMem(ptr);
        }
    }

    #endregion
}
