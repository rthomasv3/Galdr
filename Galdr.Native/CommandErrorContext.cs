using System;

namespace Galdr.Native;

/// <summary>
/// Context passed to the <see cref="GaldrOptions.OnCommandError"/> hook when an exception
/// escapes a galdrInvoke command handler. The error has already been serialized and returned
/// to the frontend — this context is for logging, telemetry, or other side effects.
/// </summary>
public sealed class CommandErrorContext
{
    /// <summary>
    /// The name of the command that threw, or <c>null</c> if the failure occurred before
    /// the command name could be parsed from the invocation payload.
    /// </summary>
    public string CommandName { get; init; }

    /// <summary>
    /// The exception thrown by the command handler (or by argument parsing).
    /// </summary>
    public Exception Exception { get; init; }
}
