using System;
using System.Runtime.InteropServices;

namespace Galdr.Native;

/// <summary>
/// P/Invoke bindings for GTK3 widget traversal.
/// </summary>
internal static class GTK3Bindings
{
    private const string GTK3Lib = "libgtk-3";
    private const string GObjectLib = "libgobject-2.0";
    private const string GLibLib = "libglib-2.0";
    private const string GdkLib = "libgdk-3";

    /// <summary>
    /// GDK window state flags as returned by <see cref="gdk_window_get_state"/>.
    /// Mirrors the <c>GdkWindowState</c> enum from gdkwindow.h.
    /// </summary>
    [Flags]
    internal enum GdkWindowState : uint
    {
        Withdrawn = 1 << 0,
        Iconified = 1 << 1,
        Maximized = 1 << 2,
        Sticky = 1 << 3,
        Fullscreen = 1 << 4,
        Above = 1 << 5,
        Below = 1 << 6,
        Focused = 1 << 7,
        Tiled = 1 << 8,
    }

    /// <summary>
    /// Gets the type name of a GType.
    /// </summary>
    [DllImport(GObjectLib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr g_type_name(IntPtr gtype);

    /// <summary>
    /// Macro expansion for G_TYPE_FROM_INSTANCE - gets the GType from an instance.
    /// This is actually a struct field access, not a function.
    /// </summary>
    internal static IntPtr G_TYPE_FROM_INSTANCE(IntPtr instance)
    {
        if (instance == IntPtr.Zero)
            return IntPtr.Zero;

        // GObject instance starts with GTypeInstance, which has g_class as first field
        // g_class points to GTypeClass, which has g_type as first field
        IntPtr gclass = Marshal.ReadIntPtr(instance);
        if (gclass == IntPtr.Zero)
            return IntPtr.Zero;

        IntPtr gtype = Marshal.ReadIntPtr(gclass);
        return gtype;
    }

    /// <summary>
    /// Gets the child of a GTK bin container.
    /// </summary>
    [DllImport(GTK3Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr gtk_bin_get_child(IntPtr bin);

    /// <summary>
    /// Gets the children of a GTK container as a GList.
    /// </summary>
    [DllImport(GTK3Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr gtk_container_get_children(IntPtr container);

    /// <summary>
    /// GList data pointer.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct GList
    {
        public IntPtr data;
        public IntPtr next;
        public IntPtr prev;
    }

    /// <summary>
    /// Helper to iterate through a GList.
    /// </summary>
    internal static System.Collections.Generic.IEnumerable<IntPtr> IterateGList(IntPtr glist)
    {
        IntPtr current = glist;
        while (current != IntPtr.Zero)
        {
            GList list = Marshal.PtrToStructure<GList>(current);
            if (list.data != IntPtr.Zero)
                yield return list.data;
            current = list.next;
        }
    }

    /// <summary>
    /// Frees a GList.
    /// </summary>
    [DllImport(GLibLib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void g_list_free(IntPtr list);

    /// <summary>
    /// Presents a window to the user, raising it to the top and (on supporting compositors) activating it.
    /// </summary>
    [DllImport(GTK3Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gtk_window_present(IntPtr window);

    /// <summary>
    /// Asks to un-minimize the given window; the window manager may or may not honor the request.
    /// </summary>
    [DllImport(GTK3Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gtk_window_deiconify(IntPtr window);

    /// <summary>
    /// Connects a callback to a GObject signal. Returns the handler ID.
    /// </summary>
    [DllImport(GObjectLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "g_signal_connect_data")]
    internal static extern ulong g_signal_connect_data(
        IntPtr instance,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string detailedSignal,
        IntPtr handler,
        IntPtr data,
        IntPtr destroyData,
        int connectFlags);

    [DllImport(GTK3Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gtk_window_get_size(IntPtr window, out int width, out int height);

    [DllImport(GTK3Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gtk_window_get_position(IntPtr window, out int rootX, out int rootY);

    [DllImport(GTK3Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gtk_window_resize(IntPtr window, int width, int height);

    [DllImport(GTK3Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gtk_window_move(IntPtr window, int x, int y);

    [DllImport(GTK3Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gtk_window_maximize(IntPtr window);

    [DllImport(GTK3Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gtk_window_unmaximize(IntPtr window);

    [DllImport(GTK3Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gtk_window_iconify(IntPtr window);

    [DllImport(GTK3Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gtk_window_fullscreen(IntPtr window);

    [DllImport(GTK3Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gtk_window_unfullscreen(IntPtr window);

    /// <summary>
    /// Returns the GdkWindow associated with a GtkWidget once it has been realized.
    /// </summary>
    [DllImport(GTK3Lib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr gtk_widget_get_window(IntPtr widget);

    /// <summary>
    /// Returns the bitmask of <see cref="GdkWindowState"/> currently set on a GdkWindow.
    /// Returns zero before the window is realized.
    /// </summary>
    [DllImport(GdkLib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern GdkWindowState gdk_window_get_state(IntPtr window);

    /// <summary>
    /// Returns the default GdkDisplay for the current process. Used to detect whether
    /// we're running under X11 or Wayland by inspecting the resulting object's GType.
    /// </summary>
    [DllImport(GdkLib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr gdk_display_get_default();

    /// <summary>
    /// Detects whether the current GTK process is running under Wayland.
    /// Position queries and moves silently no-op on Wayland by protocol design,
    /// so callers should suppress those operations when this returns true.
    /// </summary>
    internal static bool IsWayland()
    {
        bool wayland = false;

        try
        {
            IntPtr display = gdk_display_get_default();

            if (display != IntPtr.Zero)
            {
                IntPtr displayType = G_TYPE_FROM_INSTANCE(display);
                IntPtr typeNamePtr = g_type_name(displayType);

                if (typeNamePtr != IntPtr.Zero)
                {
                    string typeName = Marshal.PtrToStringAnsi(typeNamePtr);
                    wayland = typeName == "GdkWaylandDisplay";
                }
            }
        }
        catch
        {
            // Fall back to environment-variable hint if GDK introspection blew up.
            string sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
            wayland = sessionType == "wayland";
        }

        return wayland;
    }
}
