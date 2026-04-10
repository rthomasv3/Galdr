using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Galdr.Native;

/// <summary>
/// Serves content from a local folder without running an HTTP server.
/// On Windows, uses WebView2's virtual host mapping.
/// On macOS, uses WKWebView's loadFileURL:allowingReadAccessToDirectory:.
/// On Linux, configures WebKitGTK to allow file access from file URLs.
/// </summary>
public sealed class FolderContent : IWebviewContent, IWebviewContentSetup
{
    #region Fields

    private readonly string _folderPath;
    private readonly string _hostname;
    private readonly string _entryFile;
    private bool _handlesNavigation;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new instance of the <see cref="FolderContent"/> class.
    /// </summary>
    /// <param name="folderPath">Absolute path to the folder containing the web content.</param>
    /// <param name="entryFile">The entry point file (e.g., "index.html").</param>
    /// <param name="hostname">Virtual hostname for Windows WebView2 mapping.</param>
    public FolderContent(string folderPath, string entryFile = "index.html", string hostname = "app.local")
    {
        _folderPath = Path.GetFullPath(folderPath);
        _entryFile = entryFile;
        _hostname = hostname;
        _handlesNavigation = false;
    }

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public bool HandlesNavigation => _handlesNavigation;

    /// <inheritdoc />
    public void Setup(GaldrWebview webview)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            SetupWindows(webview);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            SetupMacOS(webview);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            SetupLinux(webview);
        }
    }

    /// <inheritdoc />
    public string ToWebviewUrl()
    {
        string url;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            url = $"https://{_hostname}/{_entryFile}";
        }
        else
        {
            string filePath = Path.Combine(_folderPath, _entryFile);
            url = "file:///" + filePath.Replace('\\', '/');
        }

        return url;
    }

    #endregion

    #region Private Methods — Windows

    private void SetupWindows(GaldrWebview webview)
    {
        IntPtr controllerPtr = webview.GetNativeHandle(WebviewNativeHandleKind.BrowserController);

        if (controllerPtr == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to get browser controller handle.");
        }

        IntPtr coreWebView2 = ComVtableCall_GetCoreWebView2(controllerPtr);

        Guid iid = new Guid("A0D6DF20-3B92-416D-AA0C-437A9C727857");
        IntPtr coreWebView2_3 = ComVtableCall_QueryInterface(coreWebView2, iid);

        // accessKind = 2 (COREWEBVIEW2_HOST_RESOURCE_ACCESS_KIND_DENY_CORS_PREFLIGHT)
        ComVtableCall_SetVirtualHostNameToFolderMapping(coreWebView2_3, _hostname, _folderPath, 2);

        ComVtableCall_Release(coreWebView2_3);
    }

    private static unsafe IntPtr ComVtableCall_GetCoreWebView2(IntPtr controller)
    {
        // ICoreWebView2Controller vtable slot 25: get_CoreWebView2
        IntPtr vtable = *(IntPtr*)controller;
        IntPtr fnPtr = *((IntPtr*)vtable + 25);
        IntPtr result;
        int hr = ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)fnPtr)(controller, &result);
        Marshal.ThrowExceptionForHR(hr);
        return result;
    }

    private static unsafe IntPtr ComVtableCall_QueryInterface(IntPtr obj, Guid iid)
    {
        // IUnknown vtable slot 0: QueryInterface
        IntPtr vtable = *(IntPtr*)obj;
        IntPtr fnPtr = *((IntPtr*)vtable + 0);
        IntPtr result;
        int hr = ((delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int>)fnPtr)(obj, &iid, &result);
        Marshal.ThrowExceptionForHR(hr);
        return result;
    }

    private static unsafe void ComVtableCall_SetVirtualHostNameToFolderMapping(
        IntPtr coreWebView2_3, string hostName, string folderPath, int accessKind)
    {
        // ICoreWebView2_3 vtable slot 71: SetVirtualHostNameToFolderMapping
        IntPtr vtable = *(IntPtr*)coreWebView2_3;
        IntPtr fnPtr = *((IntPtr*)vtable + 71);

        fixed (char* hostNamePtr = hostName)
        fixed (char* folderPathPtr = folderPath)
        {
            int hr = ((delegate* unmanaged[Stdcall]<IntPtr, char*, char*, int, int>)fnPtr)(
                coreWebView2_3, hostNamePtr, folderPathPtr, accessKind);
            Marshal.ThrowExceptionForHR(hr);
        }
    }

    private static unsafe void ComVtableCall_Release(IntPtr obj)
    {
        // IUnknown vtable slot 2: Release
        IntPtr vtable = *(IntPtr*)obj;
        IntPtr fnPtr = *((IntPtr*)vtable + 2);
        ((delegate* unmanaged[Stdcall]<IntPtr, uint>)fnPtr)(obj);
    }

    #endregion

    #region Private Methods — macOS

    private void SetupMacOS(GaldrWebview webview)
    {
        // WKWebView's loadFileURL:allowingReadAccessToURL: handles navigation and
        // grants the webview process read access to the folder. This replaces the
        // normal webview_navigate call.
        IntPtr wkWebView = webview.GetNativeHandle(WebviewNativeHandleKind.UIWidget);

        if (wkWebView == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to get WKWebView handle.");
        }

        string filePath = Path.Combine(_folderPath, _entryFile);
        IntPtr filePathNS = ObjCBindings.CreateNSString(filePath);
        IntPtr folderPathNS = ObjCBindings.CreateNSString(_folderPath);

        IntPtr fileURL = ObjCBindings.CreateNSURLFromFilePath(filePathNS);
        IntPtr folderURL = ObjCBindings.CreateNSURLFromFilePath(folderPathNS);

        // [wkWebView loadFileURL:fileURL allowingReadAccessToURL:folderURL]
        IntPtr selector = ObjCBindings.sel_registerName("loadFileURL:allowingReadAccessToURL:");
        ObjCBindings.objc_msgSend_IntPtr_IntPtr_void(wkWebView, selector, fileURL, folderURL);

        ObjCBindings.ReleaseNSObject(folderURL);
        ObjCBindings.ReleaseNSObject(fileURL);
        ObjCBindings.ReleaseNSObject(folderPathNS);
        ObjCBindings.ReleaseNSObject(filePathNS);

        _handlesNavigation = true;
    }

    #endregion

    #region Private Methods — Linux

    private void SetupLinux(GaldrWebview webview)
    {
        // Configure WebKitGTK settings to allow file:// URLs to load other
        // file:// resources (JS, CSS, etc.), then let the normal navigation
        // proceed with the file:// URL from ToWebviewUrl().
        IntPtr webkitWebView = FindWebKitWebView(webview.GetWindow());

        if (webkitWebView != IntPtr.Zero)
        {
            IntPtr settings = WebKit2GTKBindings.webkit_web_view_get_settings(webkitWebView);

            if (settings != IntPtr.Zero)
            {
                WebKit2GTKBindings.g_object_set(settings, "allow-file-access-from-file-urls", true, IntPtr.Zero);
            }
        }
    }

    private IntPtr FindWebKitWebView(IntPtr widget)
    {
        if (widget == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        IntPtr widgetType = GTK3Bindings.G_TYPE_FROM_INSTANCE(widget);
        IntPtr typeNamePtr = GTK3Bindings.g_type_name(widgetType);
        IntPtr found = IntPtr.Zero;

        if (typeNamePtr != IntPtr.Zero)
        {
            string typeName = Marshal.PtrToStringAnsi(typeNamePtr);

            if (typeName == "WebKitWebView")
            {
                found = widget;
            }
        }

        if (found == IntPtr.Zero)
        {
            IntPtr binChild = GTK3Bindings.gtk_bin_get_child(widget);

            if (binChild != IntPtr.Zero)
            {
                found = FindWebKitWebView(binChild);
            }
        }

        if (found == IntPtr.Zero)
        {
            IntPtr children = GTK3Bindings.gtk_container_get_children(widget);

            if (children != IntPtr.Zero)
            {
                try
                {
                    foreach (IntPtr child in GTK3Bindings.IterateGList(children))
                    {
                        found = FindWebKitWebView(child);

                        if (found != IntPtr.Zero)
                        {
                            break;
                        }
                    }
                }
                finally
                {
                    GTK3Bindings.g_list_free(children);
                }
            }
        }

        return found;
    }

    #endregion
}
