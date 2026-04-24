using System;

namespace Galdr.Native;

/// <summary>
/// Context passed to the <see cref="GaldrOptions.OnUnhandledException"/> hook when an
/// exception escapes the command pipeline — either terminating the process via
/// <see cref="AppDomain.UnhandledException"/>, or silently faulting a <see cref="System.Threading.Tasks.Task"/>
/// observed via <see cref="System.Threading.Tasks.TaskScheduler.UnobservedTaskException"/>.
/// </summary>
public sealed class UnhandledExceptionContext
{
    /// <summary>
    /// The exception that escaped. May be <c>null</c> in rare cases where the runtime
    /// reports a non-<see cref="Exception"/>-derived error object via <see cref="AppDomain.UnhandledException"/>.
    /// </summary>
    public Exception Exception { get; init; }

    /// <summary>
    /// <c>true</c> if the CLR is about to terminate the process as a result of this
    /// exception. Always <c>false</c> for <see cref="UnhandledExceptionSource.TaskScheduler"/>.
    /// </summary>
    public bool IsTerminating { get; init; }

    /// <summary>
    /// Which runtime event produced this exception. Useful for routing (e.g. logging
    /// <see cref="UnhandledExceptionSource.AppDomain"/> at ERROR and
    /// <see cref="UnhandledExceptionSource.TaskScheduler"/> at WARN).
    /// </summary>
    public UnhandledExceptionSource Source { get; init; }
}
