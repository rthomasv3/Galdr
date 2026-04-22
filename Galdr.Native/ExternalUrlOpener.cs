using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Galdr.Native;

/// <summary>
/// Opens URLs in the user's default browser using platform-native, sandbox-safe APIs.
/// </summary>
internal static class ExternalUrlOpener
{
    #region Fields

    private static readonly string[] LinuxOpeners =
    {
        "xdg-open",
        "gio",
        "gnome-open",
        "kde-open",
        "wslview",
    };

    #endregion

    #region Public Methods

    /// <summary>
    /// Opens the given URL in the system's default handler. Only http and https
    /// schemes are allowed. Silently no-ops for invalid or unsupported URLs.
    /// </summary>
    public static void Open(string url)
    {
        if (!String.IsNullOrWhiteSpace(url) &&
            Uri.TryCreate(url, UriKind.Absolute, out Uri uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                OpenOnMac(uri.AbsoluteUri);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                OpenOnLinux(uri.AbsoluteUri);
            }
            else
            {
                OpenOnWindows(uri.AbsoluteUri);
            }
        }
    }

    #endregion

    #region Private Methods

    private static void OpenOnWindows(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private static void OpenOnLinux(string url)
    {
        bool launched = false;
        int index = 0;

        while (!launched && index < LinuxOpeners.Length)
        {
            string opener = LinuxOpeners[index];
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = opener,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            if (opener == "gio")
            {
                startInfo.ArgumentList.Add("open");
                startInfo.ArgumentList.Add(url);
            }
            else
            {
                startInfo.ArgumentList.Add(url);
            }

            try
            {
                Process process = Process.Start(startInfo);
                launched = process != null;
            }
            catch
            {
                // Opener not installed; try the next one.
            }

            index++;
        }
    }

    private static void OpenOnMac(string url)
    {
        IntPtr nsUrlString = IntPtr.Zero;

        try
        {
            nsUrlString = ObjCBindings.CreateNSString(url);

            IntPtr nsUrlClass = ObjCBindings.objc_getClass("NSURL");
            IntPtr urlWithStringSel = ObjCBindings.sel_registerName("URLWithString:");
            IntPtr nsUrl = ObjCBindings.objc_msgSend_IntPtr_IntPtr(nsUrlClass, urlWithStringSel, nsUrlString);

            if (nsUrl != IntPtr.Zero)
            {
                IntPtr workspaceClass = ObjCBindings.objc_getClass("NSWorkspace");
                IntPtr sharedWorkspaceSel = ObjCBindings.sel_registerName("sharedWorkspace");
                IntPtr openUrlSel = ObjCBindings.sel_registerName("openURL:");

                IntPtr workspace = ObjCBindings.objc_msgSend_IntPtr(workspaceClass, sharedWorkspaceSel);
                ObjCBindings.objc_msgSend_bool_IntPtr(workspace, openUrlSel, nsUrl);
            }
        }
        finally
        {
            ObjCBindings.ReleaseNSObject(nsUrlString);
        }
    }

    #endregion
}
