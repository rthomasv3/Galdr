using Foundation;
using ObjCRuntime;
using UIKit;

namespace Galdr.Native;

/// <summary>
/// The framework-provided iOS application delegate. <see cref="Galdr.Run"/> passes this
/// type to <c>UIApplication.Main</c>; it wires the scene lifecycle to
/// <see cref="GaldrSceneDelegate"/> programmatically, so app Info.plist files need no
/// scene manifest.
/// </summary>
[Register("GaldrAppDelegate")]
public class GaldrAppDelegate : UIApplicationDelegate
{
    /// <summary>
    /// The app instance handed off by <see cref="Galdr.Run"/> immediately before
    /// UIApplication.Main enters the native loop.
    /// </summary>
    internal static Galdr Current;

    /// <inheritdoc />
    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        return true;
    }

    /// <inheritdoc />
    public override UISceneConfiguration GetConfiguration(UIApplication application, UISceneSession connectingSceneSession, UISceneConnectionOptions options)
    {
        return new UISceneConfiguration(null, connectingSceneSession.Role)
        {
            DelegateClass = new Class(typeof(GaldrSceneDelegate)),
        };
    }
}
