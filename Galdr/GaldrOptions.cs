using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SharpWebview.Content;

namespace Galdr;

public sealed class GaldrOptions
{
    public string Title { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public int MinWidth { get; init; }
    public int MinHeight { get; init; }
    public bool Debug { get; init; }
    public ServiceCollection Services { get; init; }
    public IWebviewContent Content { get; init; }
    public Dictionary<string, MethodInfo> Commands { get; init; }
}
