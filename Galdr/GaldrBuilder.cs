using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SharpWebview.Content;

namespace Galdr;

public sealed class GaldrBuilder
{
    #region Fields

    private readonly ServiceCollection _services = new ServiceCollection();

    private string _title = "Galdr";
    private int _width = 1024;
    private int _height = 768;
    private int _minWidth = 800;
    private int _minHeight = 600;
    private int _port = 42069;
    private bool _debug = false;
    private string _commandNamespace = "Commands";

    #endregion

    #region Public Methods

    /// <summary>
    /// Set the application window title.
    /// </summary>
    public GaldrBuilder SetTitle(string title)
    {
        _title = title;
        return this;
    }

    /// <summary>
    /// Set the application window width and height.
    /// </summary>
    public GaldrBuilder SetSize(int width, int height)
    {
        _width = width;
        _height = height;
        return this;
    }

    /// <summary>
    /// Set the minimum width and height of the application window.
    /// </summary>
    public GaldrBuilder SetMinSize(int minWidth, int minHeight)
    {
        _minWidth = minWidth;
        _minHeight = minHeight;
        return this;
    }

    /// <summary>
    /// Sets the port on which the web application will be served.
    /// </summary>
    public GaldrBuilder SetPort(int port)
    {
        _port = port;
        return this;
    }

    /// <summary>
    /// Set to true to activate a debug view (if the current webview implementation supports it).
    /// </summary>
    public GaldrBuilder SetDebug(bool debug)
    {
        _debug = debug;
        return this;
    }

    /// <summary>
    /// Sets the namespace to search for methods with the <see cref="CommandAttribute"/>.
    /// </summary>
    public GaldrBuilder SetCommandNamespace(string commandNamespace)
    {
        _commandNamespace = commandNamespace;
        return this;
    }

    /// <summary>
    /// Adds a service with a transient lifetime to the services collection for use in dependency injection.
    /// </summary>
    public GaldrBuilder AddService<T>() 
        where T : class
    {
        _services.AddTransient<T>();
        return this;
    }

    /// <summary>
    /// Adds a service with a transient lifetime to the services collection for use in dependency injection.
    /// </summary>
    public GaldrBuilder AddService<T1, T2>()
        where T1 : class
        where T2 : class, T1
    {
        _services.AddTransient<T1, T2>();
        return this;
    }

    /// <summary>
    /// Adds a service with a singleton lifetime to the services collection for use in dependency injection.
    /// </summary>
    public GaldrBuilder AddSingleton<T>()
        where T : class
    {
        _services.AddSingleton<T>();
        return this;
    }

    /// <summary>
    /// Adds a service with a singleton lifetime to the services collection for use in dependency injection.
    /// </summary>
    public GaldrBuilder AddSingleton<T1, T2>()
        where T1 : class
        where T2 : class, T1
    {
        _services.AddSingleton<T1, T2>();
        return this;
    }

    /// <summary>
    /// Builds a new instance of the <see cref="Galdr"/> class with the configured options.
    /// </summary>
    /// <remarks>
    /// Requires the threading model for the application to be single-threaded apartment (<see cref="STAThreadAttribute"/>).
    /// </remarks>
    /// <exception cref="AccessViolationException">
    /// Thrown when the threading model for the application is not single-threaded apartment (<see cref="STAThreadAttribute"/>).
    /// </exception>
    public Galdr Build()
    {
        return new Galdr(new GaldrOptions()
        {
            Commands = GetCommands(),
            Content = new EmbeddedContent(_port),
            Debug = _debug,
            Height = _height,
            MinHeight = _minHeight,
            MinWidth = _minWidth,
            Services = _services,
            Title = _title,
            Width = _width,
        });
    }

    #endregion

    #region Private Methods

    private IWebviewContent GetContent()
    {
        bool serverIsRunning = false;

        try
        {
            using TcpClient client = new();
            IAsyncResult result = client.BeginConnect("localhost", _port, null, null);
            bool success = result.AsyncWaitHandle.WaitOne(50);
            client.EndConnect(result);
            serverIsRunning = true;
        }
        catch (SocketException) { }

        return serverIsRunning ? new UrlContent($"http://localhost:{_port}") : new EmbeddedContent(_port);
    }

    private Dictionary<string, MethodInfo> GetCommands()
    {
        Dictionary<string, MethodInfo> commandMap = new Dictionary<string, MethodInfo>();

        IEnumerable<Type> commandTypes = Assembly
            .GetEntryAssembly()
            .GetTypes()
            .Where(x => x.Namespace?.Contains(_commandNamespace) == true);

        foreach (Type commandType in commandTypes)
        {
            IEnumerable<MethodInfo> commands = commandType
                .GetMethods()
                .Where(x => x.IsPublic &&
                            x.CustomAttributes.Any(y => y.AttributeType == typeof(CommandAttribute)));

            foreach (MethodInfo command in commands)
            {
                CommandAttribute commandAttribute = command.GetCustomAttribute<CommandAttribute>();

                string name = String.IsNullOrWhiteSpace(commandAttribute.Name) ? command.Name : commandAttribute.Name;
                name = Char.ToLowerInvariant(name[0]) + name.Substring(1);

                if (!commandMap.ContainsKey(name))
                {
                    commandMap.Add(name, command);
                }
            }
        }

        return commandMap;
    }

    #endregion
}
