using System;
using System.Collections.Generic;
using GaldrJson;
using Microsoft.Extensions.DependencyInjection;

namespace Galdr.Native;

/// <summary>
/// Class used to define the configuration for a <see cref="Galdr"/> instance.
/// </summary>
public sealed class GaldrOptions
{
    /// <summary>
    /// The title of the application window.
    /// </summary>
    public string Title { get; init; }

    /// <summary>
    /// Default for the application window width.
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Default for the application window height.
    /// </summary>
    public int Height { get; init; }

    /// <summary>
    /// Minimum bound for application window width.
    /// </summary>
    public int MinWidth { get; init; }

    /// <summary>
    /// Minimum bound for application window height.
    /// </summary>
    public int MinHeight { get; init; }

    /// <summary>
    /// Set to true to activate a debug view (if the current webview implementation supports it).
    /// </summary>
    public bool Debug { get; init; }

    /// <summary>
    /// The collection of services for use in executing commands and injecting dependencies.
    /// </summary>
    public IServiceCollection Services { get; init; }

    /// <summary>
    /// The port the content is being served from.
    /// </summary>
    public int Port { get; init; }

    /// <summary>
    /// A dictionary mapping a name for use in <c>galdrInvoke</c> with the associated C# method to call.
    /// </summary>
    public Dictionary<string, CommandInfo> Commands { get; init; }

    /// <summary>
    /// Sets a custom content provider for the webview.
    /// </summary>
    public IWebviewContent ContentProvider { get; init; }

    /// <summary>
    /// A value indicating if a loading screen should be shown on launch.
    /// </summary>
    public bool ShowLoading { get; init; }

    /// <summary>
    /// A message to show on the loading page.
    /// </summary>
    public string LoadingMessage { get; init; }

    /// <summary>
    /// The background color to use on the loading page.
    /// </summary>
    public string LoadingBackground { get; init; }

    /// <summary>
    /// Languages to enable spell checking for (ex. en_US).
    /// </summary>
    public List<string> SpellCheckingLanguages { get; init; }

    /// <summary>
    /// Injects JavaScript code at the initialization of the new page.
    /// </summary>
    public string InitScript { get; init; }

    /// <summary>
    /// The JSON serializer used for Galdr-specific serialization operations.
    /// </summary>
    internal IGaldrJsonSerializer GaldrJsonSerializer { get; init; }

    /// <summary>
    /// The JSON serialization options used in the serialization and deserialization process.
    /// </summary>
    internal GaldrJsonOptions GaldrJsonOptions { get; init; }

    /// <summary>
    /// If set, the app will enforce that only one instance runs per machine-user. A duplicate
    /// launch notifies the primary (which focuses its window and fires <see cref="SecondInstance"/>)
    /// and exits without creating a webview.
    /// </summary>
    public string SingleInstanceAppId { get; init; }

    /// <summary>
    /// Runs in the primary process before the webview is constructed or services are built.
    /// Use for work that must precede UI init (e.g. database migrations, file prep).
    /// </summary>
    public Action BeforeStartup { get; init; }

    /// <summary>
    /// Runs in the primary process after services are built and the webview is constructed,
    /// but before the main loop starts. <see cref="Galdr.GetWindow"/> is valid here.
    /// </summary>
    public Action<IServiceProvider> Startup { get; init; }

    /// <summary>
    /// Dispatched onto the UI thread after the main loop has started.
    /// </summary>
    public Action<IServiceProvider> AfterStartup { get; init; }

    /// <summary>
    /// Called on the UI thread when the user attempts to close the window. If registered,
    /// the close is cancelled automatically — the handler must call <see cref="Galdr.Terminate"/>
    /// to actually close the application when ready. This enables async save workflows:
    /// dispatch JS to the frontend, wait for a callback, then terminate.
    /// </summary>
    public Action<Galdr> BeforeClose { get; init; }

    /// <summary>
    /// Runs in the primary process (on the UI thread) when a duplicate launch is detected.
    /// The window has already been focused by the time this fires. Arguments are the command-line
    /// args of the duplicate process; cwd is its current working directory.
    /// </summary>
    public Action<string[], string> SecondInstance { get; init; }

    /// <summary>
    /// Fires when an exception escapes a galdrInvoke command handler. The error has already
    /// been serialized and returned to the frontend — this hook exists for logging and
    /// telemetry. The service provider is passed so the hook can resolve app services (such
    /// as a logger) without capturing state from <see cref="Startup"/>. Exceptions thrown by
    /// the hook itself are swallowed so they cannot disrupt the error response to the frontend.
    /// </summary>
    public Action<CommandErrorContext, IServiceProvider> OnCommandError { get; init; }

    /// <summary>
    /// Fires for exceptions that escape the command pipeline — either terminating
    /// <see cref="AppDomain.UnhandledException"/> errors or silently-faulting tasks surfaced
    /// via <see cref="System.Threading.Tasks.TaskScheduler.UnobservedTaskException"/>.
    /// Subscribed at startup and unsubscribed on dispose. The service provider may be
    /// <c>null</c> if the exception fires before services are built. Exceptions thrown by
    /// the hook itself are swallowed.
    /// </summary>
    public Action<UnhandledExceptionContext, IServiceProvider> OnUnhandledException { get; init; }

    /// <summary>
    /// Fires after the user has finished resizing, moving, or changing the state of the
    /// window. Calls are debounced internally (~250 ms) and dispatched onto the UI thread,
    /// so this hook is safe to use for persistence and other non-trivial work without
    /// throttling burst events from a drag. The hook fires for every state including
    /// <see cref="WindowState.Minimized"/> — filter inside the handler if you do not want
    /// to persist a minimized window. Position is reported in top-left screen coordinates
    /// on Windows and X11; on Wayland the X/Y components of <see cref="WindowChangedContext"/>
    /// are <c>null</c> because the Wayland protocol does not expose absolute window position
    /// to clients. The handler must not throw; exceptions are swallowed.
    /// </summary>
    public Action<Galdr, WindowChangedContext, IServiceProvider> WindowChanged { get; init; }

    /// <summary>
    /// Mutable bridge that carries the service provider built inside <see cref="Galdr.Run"/>
    /// back to the builder's command-parameter resolver. Populated by Galdr after the
    /// provider is built; consumed by command handlers captured at registration time.
    /// </summary>
    internal ServiceProviderAccessor ServiceProviderAccessor { get; init; }
}
