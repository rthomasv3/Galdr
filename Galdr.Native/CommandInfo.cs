using System;
using System.Threading.Tasks;

namespace Galdr.Native;

/// <summary>
/// Class used to represent commands available to call with galdrInvoke.
/// </summary>
public sealed class CommandInfo
{
    /// <summary>
    /// The handler function to run.
    /// </summary>
    public Func<string, Task<object>> Handler { get; set; }

    /// <summary>
    /// The return type for the function.
    /// </summary>
    public Type ResultType { get; set; }
}
