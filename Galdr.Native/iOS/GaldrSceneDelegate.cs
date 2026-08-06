using Foundation;
using UIKit;

namespace Galdr.Native;

/// <summary>
/// The framework-provided window scene delegate: creates the window with the Galdr
/// view controller on connect and relays the background/foreground transitions to the
/// <see cref="GaldrBuilder.OnBackground"/> / <see cref="GaldrBuilder.OnResume"/> hooks.
/// </summary>
[Register("GaldrSceneDelegate")]
public class GaldrSceneDelegate : UIResponder, IUIWindowSceneDelegate
{
    /// <inheritdoc cref="IUIWindowSceneDelegate" />
    [Export("window")]
    public UIWindow Window { get; set; }

    /// <summary>
    /// Attaches the Galdr view controller to the connecting window scene.
    /// </summary>
    [Export("scene:willConnectToSession:options:")]
    public void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
    {
        if (scene is UIWindowScene windowScene)
        {
            Window ??= new UIWindow(windowScene);
            Window.RootViewController = new GaldrViewController(GaldrAppDelegate.Current);
            Window.MakeKeyAndVisible();
        }
    }

    /// <summary>
    /// The mobile "save now" moment — fires the OnBackground hook.
    /// </summary>
    [Export("sceneDidEnterBackground:")]
    public void DidEnterBackground(UIScene scene)
    {
        GaldrAppDelegate.Current?.InvokeBackground();
    }

    /// <summary>
    /// Foreground return — fires the OnResume hook (not on initial launch).
    /// </summary>
    [Export("sceneWillEnterForeground:")]
    public void WillEnterForeground(UIScene scene)
    {
        GaldrAppDelegate.Current?.InvokeResume();
    }
}
