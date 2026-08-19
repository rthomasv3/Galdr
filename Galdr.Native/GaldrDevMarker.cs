using System;
using System.IO;
using System.Text.Json;

namespace Galdr.Native;

/// <summary>
/// Reads the <c>galdr.dev.json</c> marker that the Galdr.Native build targets stage
/// into Debug builds. Marker presence is how the framework knows at runtime that the
/// app was built Debug — it drives <see cref="MultiplatformContent"/>'s dev-server
/// behavior and the default for <see cref="GaldrBuilder.SetDebug"/>.
/// </summary>
internal static class GaldrDevMarker
{
    private const string FileName = "galdr.dev.json";

    /// <summary>
    /// Returns whether the marker exists (i.e. this is a Debug build). When present,
    /// <paramref name="hostIp"/> carries the stamped build-machine LAN address, or
    /// <c>null</c> if the build couldn't determine one.
    /// </summary>
    public static bool TryRead(out string hostIp)
    {
        hostIp = null;

        string json = ReadJson();
        bool present = json != null;

        if (present)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(json);

                if (document.RootElement.TryGetProperty("hostIp", out JsonElement hostElement))
                {
                    string value = hostElement.GetString();
                    hostIp = String.IsNullOrEmpty(value) ? null : value;
                }
            }
            catch
            {
                // A malformed marker still marks a Debug build; it just has no address.
            }
        }

        return present;
    }

#if ANDROID
    private static string ReadJson()
    {
        string json = null;

        try
        {
            using Stream stream = Android.App.Application.Context.Assets.Open(FileName);
            using StreamReader reader = new StreamReader(stream);
            json = reader.ReadToEnd();
        }
        catch
        {
            // Asset not present — Release build.
        }

        return json;
    }
#elif IOS
    private static string ReadJson()
    {
        string path = Path.Combine(Foundation.NSBundle.MainBundle.BundlePath, FileName);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }
#else
    private static string ReadJson()
    {
        string path = Path.Combine(AppContext.BaseDirectory, FileName);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }
#endif
}
