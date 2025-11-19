using System;
using System.Runtime.InteropServices;

namespace Galdr;

/// <summary>
/// P/Invoke bindings for GTK3 widget traversal.
/// </summary>
internal static class GTK3Bindings
{
    private const string GTK3Lib = "libgtk-3";
    private const string GObjectLib = "libgobject-2.0";
    private const string GLibLib = "libglib-2.0";

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
}
