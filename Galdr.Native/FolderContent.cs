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
        IntPtr wkWebView = FindWKWebView(webview);

        if (wkWebView == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to find WKWebView in the window hierarchy.");
        }

        // Enable file:// access from file:// URLs. ES modules (<script type="module">)
        // always use CORS mode, which fails with origin null on file:// URLs.
        // This WKPreferences setting allows file:// sub-resources to load.
        // [wkWebView.configuration.preferences setValue:@YES forKey:@"allowFileAccessFromFileURLs"]
        IntPtr configSel = ObjCBindings.sel_registerName("configuration");
        IntPtr config = ObjCBindings.objc_msgSend_IntPtr(wkWebView, configSel);

        IntPtr prefsSel = ObjCBindings.sel_registerName("preferences");
        IntPtr prefs = ObjCBindings.objc_msgSend_IntPtr(config, prefsSel);

        IntPtr yesNumber = ObjCBindings.objc_msgSend_IntPtr_IntPtr(
            ObjCBindings.objc_getClass("NSNumber"),
            ObjCBindings.sel_registerName("numberWithBool:"),
            (IntPtr)1);
        IntPtr keyNS = ObjCBindings.CreateNSString("allowFileAccessFromFileURLs");

        IntPtr setValueSel = ObjCBindings.sel_registerName("setValue:forKey:");
        ObjCBindings.objc_msgSend_IntPtr_IntPtr_void(prefs, setValueSel, yesNumber, keyNS);

        ObjCBindings.ReleaseNSObject(keyNS);

        // Load the content via loadFileURL:allowingReadAccessToURL: which grants
        // the WKWebView process read access to the content folder.
        string filePath = Path.Combine(_folderPath, _entryFile);
        IntPtr filePathNS = ObjCBindings.CreateNSString(filePath);
        IntPtr folderPathNS = ObjCBindings.CreateNSString(_folderPath);

        IntPtr fileURL = ObjCBindings.CreateNSURLFromFilePath(filePathNS);
        IntPtr folderURL = ObjCBindings.CreateNSURLFromFilePath(folderPathNS);

        IntPtr loadSel = ObjCBindings.sel_registerName("loadFileURL:allowingReadAccessToURL:");
        ObjCBindings.objc_msgSend_IntPtr_IntPtr_void(wkWebView, loadSel, fileURL, folderURL);

        ObjCBindings.ReleaseNSObject(folderURL);
        ObjCBindings.ReleaseNSObject(fileURL);
        ObjCBindings.ReleaseNSObject(folderPathNS);
        ObjCBindings.ReleaseNSObject(filePathNS);

        _handlesNavigation = true;
    }

    /// <summary>
    /// Finds the WKWebView by first trying the native handle, then walking the
    /// NSWindow's view hierarchy. Some versions of the webview library return an
    /// NSWindow or NSView from UIWidget instead of the WKWebView itself.
    /// </summary>
    private static IntPtr FindWKWebView(GaldrWebview webview)
    {
        // First try UIWidget — some versions return the WKWebView directly.
        IntPtr widget = webview.GetNativeHandle(WebviewNativeHandleKind.UIWidget);

        if (widget != IntPtr.Zero && IsWKWebView(widget))
        {
            return widget;
        }

        // Fall back to walking the view hierarchy from the window.
        IntPtr window = webview.GetWindow();

        if (window == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        IntPtr contentViewSel = ObjCBindings.sel_registerName("contentView");
        IntPtr contentView = ObjCBindings.objc_msgSend_IntPtr(window, contentViewSel);

        if (contentView == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        if (IsWKWebView(contentView))
        {
            return contentView;
        }

        return FindWKWebViewInSubviews(contentView);
    }

    /// <summary>
    /// Recursively searches through NSView subviews to find the WKWebView.
    /// </summary>
    private static IntPtr FindWKWebViewInSubviews(IntPtr view)
    {
        IntPtr subviewsSel = ObjCBindings.sel_registerName("subviews");
        IntPtr subviews = ObjCBindings.objc_msgSend_IntPtr(view, subviewsSel);

        if (subviews == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        IntPtr countSel = ObjCBindings.sel_registerName("count");
        long count = ObjCBindings.objc_msgSend_long(subviews, countSel);

        IntPtr objectAtIndexSel = ObjCBindings.sel_registerName("objectAtIndex:");

        for (long i = 0; i < count; i++)
        {
            IntPtr subview = ObjCBindings.objc_msgSend_IntPtr_IntPtr(subviews, objectAtIndexSel, (IntPtr)i);

            if (IsWKWebView(subview))
            {
                return subview;
            }

            IntPtr found = FindWKWebViewInSubviews(subview);

            if (found != IntPtr.Zero)
            {
                return found;
            }
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Checks whether an Objective-C object is a WKWebView (or subclass).
    /// </summary>
    private static bool IsWKWebView(IntPtr obj)
    {
        if (obj == IntPtr.Zero)
        {
            return false;
        }

        IntPtr wkWebViewClass = ObjCBindings.objc_getClass("WKWebView");

        if (wkWebViewClass == IntPtr.Zero)
        {
            return false;
        }

        IntPtr isKindOfClassSel = ObjCBindings.sel_registerName("isKindOfClass:");
        return ObjCBindings.objc_msgSend_bool_IntPtr(obj, isKindOfClassSel, wkWebViewClass);
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
