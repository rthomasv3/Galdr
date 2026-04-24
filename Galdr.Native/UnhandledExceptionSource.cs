namespace Galdr.Native;

/// <summary>
/// Identifies which runtime event produced an unhandled exception reported to the
/// <see cref="GaldrOptions.OnUnhandledException"/> hook.
/// </summary>
public enum UnhandledExceptionSource
{
    /// <summary>
    /// Raised by <see cref="System.AppDomain.UnhandledException"/>. The process is typically
    /// terminating when this fires — the hook is a last-gasp logging opportunity.
    /// </summary>
    AppDomain,

    /// <summary>
    /// Raised by <see cref="System.Threading.Tasks.TaskScheduler.UnobservedTaskException"/>.
    /// A faulted <see cref="System.Threading.Tasks.Task"/> was garbage-collected without its
    /// exception being observed. Non-fatal in .NET Core and later.
    /// </summary>
    TaskScheduler,
}
