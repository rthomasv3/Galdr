using System;
using System.Threading.Tasks;

namespace Galdr.Native;

public sealed class CommandInfo
{
    public Func<string, Task<object>> Handler { get; set; }
    public Type ResultType { get; set; }
}
