using System;
using System.Collections.Generic;
using System.IO;
using Foundation;
using WebKit;

namespace Galdr.Native;

/// <summary>
/// Serves the bundled SPA out of the app bundle under the <c>galdr://app</c> origin.
/// WKWebView can only intercept custom schemes (not http/https), so this is the iOS
/// equivalent of Android's WebViewAssetLoader: a real origin with working localStorage,
/// fetch, and history.pushState against bundled files. Extensionless paths fall back to
/// index.html for history-mode SPA routing.
/// </summary>
internal sealed class GaldrSchemeHandler : NSObject, IWKUrlSchemeHandler
{
    #region Fields

    private static readonly string _wwwroot = Path.Combine(NSBundle.MainBundle.BundlePath, "wwwroot");

    private static readonly Dictionary<string, string> _mimeTypes = new()
    {
        [".html"] = "text/html",
        [".js"] = "text/javascript",
        [".mjs"] = "text/javascript",
        [".css"] = "text/css",
        [".json"] = "application/json",
        [".svg"] = "image/svg+xml",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".ico"] = "image/x-icon",
        [".woff"] = "font/woff",
        [".woff2"] = "font/woff2",
        [".ttf"] = "font/ttf",
        [".txt"] = "text/plain",
        [".wasm"] = "application/wasm",
        [".pdf"] = "application/pdf",
        [".mp3"] = "audio/mpeg",
        [".mp4"] = "video/mp4",
        [".webm"] = "video/webm",
    };

    #endregion

    #region Public Methods

    public void StartUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask urlSchemeTask)
    {
        NSUrl requestUrl = urlSchemeTask.Request.Url;

        try
        {
            string requestPath = requestUrl.Path ?? "/";
            string relativePath = requestPath.TrimStart('/');
            string filePath = String.IsNullOrEmpty(relativePath)
                ? Path.Combine(_wwwroot, "index.html")
                : Path.Combine(_wwwroot, relativePath.Replace('/', Path.DirectorySeparatorChar));

            // Guard against escaping the wwwroot via ../ segments.
            string fullPath = Path.GetFullPath(filePath);
            bool insideRoot = fullPath.StartsWith(_wwwroot, StringComparison.Ordinal);

            if (insideRoot && !File.Exists(fullPath) && !Path.HasExtension(fullPath))
            {
                // SPA fallback: history-mode routes resolve to the shell.
                fullPath = Path.Combine(_wwwroot, "index.html");
            }

            if (insideRoot && File.Exists(fullPath))
            {
                byte[] body = File.ReadAllBytes(fullPath);
                string mimeType = _mimeTypes.GetValueOrDefault(
                    Path.GetExtension(fullPath).ToLowerInvariant(), "application/octet-stream");

                Respond(urlSchemeTask, requestUrl, 200, mimeType, body);
            }
            else
            {
                Respond(urlSchemeTask, requestUrl, 404, "text/plain", "not found"u8.ToArray());
            }
        }
        catch (Exception e)
        {
            urlSchemeTask.DidFailWithError(new NSError(
                new NSString("GaldrSchemeHandler"), 0,
                NSDictionary.FromObjectAndKey(new NSString(e.Message), NSError.LocalizedDescriptionKey)));
        }
    }

    public void StopUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask urlSchemeTask)
    {
        // Responses are written synchronously in StartUrlSchemeTask; nothing to cancel.
    }

    #endregion

    #region Private Methods

    private static void Respond(IWKUrlSchemeTask task, NSUrl url, int status, string mimeType, byte[] body)
    {
        using NSHttpUrlResponse response = new(url, status, "HTTP/1.1", new NSMutableDictionary
        {
            [new NSString("Content-Type")] = new NSString(mimeType),
            [new NSString("Content-Length")] = new NSString(body.Length.ToString()),
        });
        task.DidReceiveResponse(response);
        task.DidReceiveData(NSData.FromArray(body));
        task.DidFinish();
    }

    #endregion
}
