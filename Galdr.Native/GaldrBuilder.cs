using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SharpWebview.Content;

namespace Galdr.Native;

/// <summary>
/// Class used to configure and build a <see cref="Galdr"/> instance.
/// </summary>
public sealed class GaldrBuilder
{
    #region Fields

    private readonly IServiceCollection _services;
    private readonly HashSet<Type> _registeredServiceTypes;

    private string _title = "Galdr";
    private int _width = 1024;
    private int _height = 768;
    private int _minWidth = 800;
    private int _minHeight = 600;
    private int _port = 0;
    private bool _debug = false;
    private Dictionary<string, CommandInfo> _commands = new();
    private IWebviewContent _contentProvider;
    private bool _showLoading;
    private string _loadingMessage;
    private string _loadingBackground;
    private List<string> _spellCheckLanguages = new();
    private string _initScript;

    private IServiceProvider _serviceProvider;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new instance of the <see cref="GaldrBuilder"/> class.
    /// </summary>
    public GaldrBuilder()
    {
        _services = new ServiceCollection()
            .AddSingleton<DialogService>()
            .AddSingleton<IDialogService, DialogService>();

        _registeredServiceTypes = [typeof(DialogService), typeof(IDialogService)];
    }

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
    /// Sets the content provider to use in web view.
    /// </summary>
    public GaldrBuilder SetContentProvider(IWebviewContent content)
    {
        _contentProvider = content;
        return this;
    }

    /// <summary>
    /// Configures Galdr to show a loading page on launch.
    /// </summary>
    public GaldrBuilder SetLoadingPage(string loadingMessage = "Loading Galdr", string backgroundColor = "#f5f5f5")
    {
        _showLoading = true;
        _loadingMessage = loadingMessage;
        _loadingBackground = backgroundColor;
        return this;
    }

    /// <summary>
    /// Enables spellchecking on Linux for the given languages (ex en_US).
    /// </summary>
    public GaldrBuilder EnableSpellChecking(params string[] languages)
    {
        _spellCheckLanguages = [.. languages];
        return this;
    }

    /// <summary>
    /// Injects JavaScript code at the initialization of the new page. Every time the
    /// webview will open a new page, this initialization code will be executed. It is
    /// guaranteed that code is executed before window.onload.
    /// </summary>
    public GaldrBuilder SetInitScript(string script)
    {
        _initScript = script;
        return this;
    }

    /// <summary>
    /// Adds a service with a transient lifetime to the services collection for use in dependency injection.
    /// </summary>
    public GaldrBuilder AddService<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>()
        where T : class
    {
        _services.AddTransient<T>();
        _registeredServiceTypes.Add(typeof(T));
        return this;
    }

    /// <summary>
    /// Adds a service with a transient lifetime to the services collection for use in dependency injection.
    /// </summary>
    public GaldrBuilder AddService<T1, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T2>()
        where T1 : class
        where T2 : class, T1
    {
        _services.AddTransient<T1, T2>();
        _registeredServiceTypes.Add(typeof(T1));
        _registeredServiceTypes.Add(typeof(T2));
        return this;
    }

    /// <summary>
    /// Adds a service with a singleton lifetime to the services collection for use in dependency injection.
    /// </summary>
    public GaldrBuilder AddSingleton<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>()
        where T : class
    {
        _services.AddSingleton<T>();
        _registeredServiceTypes.Add(typeof(T));
        return this;
    }

    /// <summary>
    /// Adds a service with a singleton lifetime to the services collection for use in dependency injection.
    /// </summary>
    public GaldrBuilder AddSingleton<T1, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T2>()
        where T1 : class
        where T2 : class, T1
    {
        _services.AddSingleton<T1, T2>();
        _registeredServiceTypes.Add(typeof(T1));
        _registeredServiceTypes.Add(typeof(T2));
        return this;
    }

    /// <summary>
    /// Adds a service instance with singleton lifetime to the services collection for use in dependency injection.
    /// </summary>
    public GaldrBuilder AddSingleton<T>(T implementationInstance)
        where T : class
    {
        _services.AddSingleton(implementationInstance);
        _registeredServiceTypes.Add(typeof(T));
        return this;
    }

    // 0 parameters

    /// <summary>
    /// Registers an action for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddAction(string name, Action handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) => { handler(); return Task.FromResult<object>(null); }
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<TResult>(string name, Func<TResult> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) => Task.FromResult<object>(handler()),
            ResultType = typeof(TResult),
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<TResult>(string name, Func<Task<TResult>> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = async (json) => await handler(),
            ResultType = typeof(TResult),
        };
    }

    // 1 parameter

    /// <summary>
    /// Registers an action for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddAction<T1>(string name, Action<T1> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) =>
            {

                object[] parameters = ParseParameters(json, typeof(T1));

                T1 p1 = (T1)parameters[0];
                handler(p1);
                return Task.FromResult<object>(null);
            }
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<T1, TResult>(string name, Func<T1, TResult> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) =>
            {

                object[] parameters = ParseParameters(json, typeof(T1));

                T1 p1 = (T1)parameters[0];
                return Task.FromResult<object>(handler(p1));
            },
            ResultType = typeof(TResult),
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<T1, TResult>(string name, Func<T1, Task<TResult>> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = async (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1));

                T1 p1 = (T1)parameters[0];
                return await handler(p1);
            },
            ResultType = typeof(TResult),
        };
    }

    // 2 parameters

    /// <summary>
    /// Registers an action for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddAction<T1, T2>(string name, Action<T1, T2> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                handler(p1, p2);
                return Task.FromResult<object>(null);
            }
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<T1, T2, TResult>(string name, Func<T1, T2, TResult> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                return Task.FromResult<object>(handler(p1, p2));
            },
            ResultType = typeof(TResult),
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<T1, T2, TResult>(string name, Func<T1, T2, Task<TResult>> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = async (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                return await handler(p1, p2);
            },
            ResultType = typeof(TResult),
        };
    }

    // 3 parameters

    /// <summary>
    /// Registers an action for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddAction<T1, T2, T3>(string name, Action<T1, T2, T3> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                handler(p1, p2, p3);
                return Task.FromResult<object>(null);
            }
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<T1, T2, T3, TResult>(string name, Func<T1, T2, T3, TResult> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                return Task.FromResult<object>(handler(p1, p2, p3));
            },
            ResultType = typeof(TResult),
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<T1, T2, T3, TResult>(string name, Func<T1, T2, T3, Task<TResult>> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = async (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                return await handler(p1, p2, p3);
            },
            ResultType = typeof(TResult),
        };
    }

    // 4 parameters

    /// <summary>
    /// Registers an action for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddAction<T1, T2, T3, T4>(string name, Action<T1, T2, T3, T4> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                handler(p1, p2, p3, p4);
                return Task.FromResult<object>(null);
            }
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<T1, T2, T3, T4, TResult>(string name, Func<T1, T2, T3, T4, TResult> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                return Task.FromResult<object>(handler(p1, p2, p3, p4));
            },
            ResultType = typeof(TResult),
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<T1, T2, T3, T4, TResult>(string name, Func<T1, T2, T3, T4, Task<TResult>> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = async (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                return await handler(p1, p2, p3, p4);
            },
            ResultType = typeof(TResult),
        };
    }

    // 5 parameters

    /// <summary>
    /// Registers an action for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddAction<T1, T2, T3, T4, T5>(string name, Action<T1, T2, T3, T4, T5> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                handler(p1, p2, p3, p4, p5);
                return Task.FromResult<object>(null);
            }
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<T1, T2, T3, T4, T5, TResult>(string name, Func<T1, T2, T3, T4, T5, TResult> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                return Task.FromResult<object>(handler(p1, p2, p3, p4, p5));
            },
            ResultType = typeof(TResult),
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<T1, T2, T3, T4, T5, TResult>(string name, Func<T1, T2, T3, T4, T5, Task<TResult>> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = async (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                return await handler(p1, p2, p3, p4, p5);
            },
            ResultType = typeof(TResult),
        };
    }

    // 6 parameters

    /// <summary>
    /// Registers an action for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddAction<T1, T2, T3, T4, T5, T6>(string name, Action<T1, T2, T3, T4, T5, T6> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                handler(p1, p2, p3, p4, p5, p6);
                return Task.FromResult<object>(null);
            }
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<T1, T2, T3, T4, T5, T6, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, TResult> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                return Task.FromResult<object>(handler(p1, p2, p3, p4, p5, p6));
            },
            ResultType = typeof(TResult),
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<T1, T2, T3, T4, T5, T6, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, Task<TResult>> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = async (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                return await handler(p1, p2, p3, p4, p5, p6);
            },
            ResultType = typeof(TResult),
        };
    }

    // 7 parameters

    /// <summary>
    /// Registers an action for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddAction<T1, T2, T3, T4, T5, T6, T7>(string name, Action<T1, T2, T3, T4, T5, T6, T7> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                T7 p7 = (T7)parameters[6];
                handler(p1, p2, p3, p4, p5, p6, p7);
                return Task.FromResult<object>(null);
            }
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<T1, T2, T3, T4, T5, T6, T7, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, TResult> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                T7 p7 = (T7)parameters[6];
                return Task.FromResult<object>(handler(p1, p2, p3, p4, p5, p6, p7));
            },
            ResultType = typeof(TResult),
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<T1, T2, T3, T4, T5, T6, T7, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, Task<TResult>> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = async (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                T7 p7 = (T7)parameters[6];
                return await handler(p1, p2, p3, p4, p5, p6, p7);
            },
            ResultType = typeof(TResult),
        };
    }

    // 8 parameters

    /// <summary>
    /// Registers an action for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddAction<T1, T2, T3, T4, T5, T6, T7, T8>(string name, Action<T1, T2, T3, T4, T5, T6, T7, T8> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                T7 p7 = (T7)parameters[6];
                T8 p8 = (T8)parameters[7];
                handler(p1, p2, p3, p4, p5, p6, p7, p8);
                return Task.FromResult<object>(null);
            }
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                T7 p7 = (T7)parameters[6];
                T8 p8 = (T8)parameters[7];
                return Task.FromResult<object>(handler(p1, p2, p3, p4, p5, p6, p7, p8));
            },
            ResultType = typeof(TResult),
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, Task<TResult>> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = async (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                T7 p7 = (T7)parameters[6];
                T8 p8 = (T8)parameters[7];
                return await handler(p1, p2, p3, p4, p5, p6, p7, p8);
            },
            ResultType = typeof(TResult),
        };
    }

    // 9 parameters

    /// <summary>
    /// Registers an action for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddAction<T1, T2, T3, T4, T5, T6, T7, T8, T9>(string name, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8), typeof(T9));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                T7 p7 = (T7)parameters[6];
                T8 p8 = (T8)parameters[7];
                T9 p9 = (T9)parameters[8];
                handler(p1, p2, p3, p4, p5, p6, p7, p8, p9);
                return Task.FromResult<object>(null);
            }
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8), typeof(T9));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                T7 p7 = (T7)parameters[6];
                T8 p8 = (T8)parameters[7];
                T9 p9 = (T9)parameters[8];
                return Task.FromResult<object>(handler(p1, p2, p3, p4, p5, p6, p7, p8, p9));
            },
            ResultType = typeof(TResult),
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, Task<TResult>> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = async (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8), typeof(T9));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                T7 p7 = (T7)parameters[6];
                T8 p8 = (T8)parameters[7];
                T9 p9 = (T9)parameters[8];
                return await handler(p1, p2, p3, p4, p5, p6, p7, p8, p9);
            },
            ResultType = typeof(TResult),
        };
    }

    // 10 parameters

    /// <summary>
    /// Registers an action for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddAction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(string name, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8), typeof(T9), typeof(T10));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                T7 p7 = (T7)parameters[6];
                T8 p8 = (T8)parameters[7];
                T9 p9 = (T9)parameters[8];
                T10 p10 = (T10)parameters[9];
                handler(p1, p2, p3, p4, p5, p6, p7, p8, p9, p10);
                return Task.FromResult<object>(null);
            }
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8), typeof(T9), typeof(T10));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                T7 p7 = (T7)parameters[6];
                T8 p8 = (T8)parameters[7];
                T9 p9 = (T9)parameters[8];
                T10 p10 = (T10)parameters[9];
                return Task.FromResult<object>(handler(p1, p2, p3, p4, p5, p6, p7, p8, p9, p10));
            },
            ResultType = typeof(TResult),
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, Task<TResult>> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = async (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8), typeof(T9), typeof(T10));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                T7 p7 = (T7)parameters[6];
                T8 p8 = (T8)parameters[7];
                T9 p9 = (T9)parameters[8];
                T10 p10 = (T10)parameters[9];
                return await handler(p1, p2, p3, p4, p5, p6, p7, p8, p9, p10);
            },
            ResultType = typeof(TResult),
        };
    }

    // 11 parameters

    /// <summary>
    /// Registers an action for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddAction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(string name, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8), typeof(T9), typeof(T10), typeof(T11));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                T7 p7 = (T7)parameters[6];
                T8 p8 = (T8)parameters[7];
                T9 p9 = (T9)parameters[8];
                T10 p10 = (T10)parameters[9];
                T11 p11 = (T11)parameters[10];
                handler(p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11);
                return Task.FromResult<object>(null);
            }
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TResult> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8), typeof(T9), typeof(T10), typeof(T11));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                T7 p7 = (T7)parameters[6];
                T8 p8 = (T8)parameters[7];
                T9 p9 = (T9)parameters[8];
                T10 p10 = (T10)parameters[9];
                T11 p11 = (T11)parameters[10];
                return Task.FromResult<object>(handler(p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11));
            },
            ResultType = typeof(TResult),
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, Task<TResult>> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = async (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8), typeof(T9), typeof(T10), typeof(T11));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                T7 p7 = (T7)parameters[6];
                T8 p8 = (T8)parameters[7];
                T9 p9 = (T9)parameters[8];
                T10 p10 = (T10)parameters[9];
                T11 p11 = (T11)parameters[10];
                return await handler(p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11);
            },
            ResultType = typeof(TResult),
        };
    }

    // 12 parameters

    /// <summary>
    /// Registers an action for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddAction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(string name, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8), typeof(T9), typeof(T10), typeof(T11), typeof(T12));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                T7 p7 = (T7)parameters[6];
                T8 p8 = (T8)parameters[7];
                T9 p9 = (T9)parameters[8];
                T10 p10 = (T10)parameters[9];
                T11 p11 = (T11)parameters[10];
                T12 p12 = (T12)parameters[11];
                handler(p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12);
                return Task.FromResult<object>(null);
            }
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TResult> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8), typeof(T9), typeof(T10), typeof(T11), typeof(T12));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                T7 p7 = (T7)parameters[6];
                T8 p8 = (T8)parameters[7];
                T9 p9 = (T9)parameters[8];
                T10 p10 = (T10)parameters[9];
                T11 p11 = (T11)parameters[10];
                T12 p12 = (T12)parameters[11];
                return Task.FromResult<object>(handler(p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12));
            },
            ResultType = typeof(TResult),
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, Task<TResult>> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = async (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8), typeof(T9), typeof(T10), typeof(T11), typeof(T12));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                T7 p7 = (T7)parameters[6];
                T8 p8 = (T8)parameters[7];
                T9 p9 = (T9)parameters[8];
                T10 p10 = (T10)parameters[9];
                T11 p11 = (T11)parameters[10];
                T12 p12 = (T12)parameters[11];
                return await handler(p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12);
            },
            ResultType = typeof(TResult),
        };
    }

    // 13 parameters

    /// <summary>
    /// Registers an action for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddAction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(string name, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8), typeof(T9), typeof(T10), typeof(T11), typeof(T12), typeof(T13));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                T7 p7 = (T7)parameters[6];
                T8 p8 = (T8)parameters[7];
                T9 p9 = (T9)parameters[8];
                T10 p10 = (T10)parameters[9];
                T11 p11 = (T11)parameters[10];
                T12 p12 = (T12)parameters[11];
                T13 p13 = (T13)parameters[12];
                handler(p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13);
                return Task.FromResult<object>(null);
            }
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TResult> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8), typeof(T9), typeof(T10), typeof(T11), typeof(T12), typeof(T13));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                T7 p7 = (T7)parameters[6];
                T8 p8 = (T8)parameters[7];
                T9 p9 = (T9)parameters[8];
                T10 p10 = (T10)parameters[9];
                T11 p11 = (T11)parameters[10];
                T12 p12 = (T12)parameters[11];
                T13 p13 = (T13)parameters[12];
                return Task.FromResult<object>(handler(p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13));
            },
            ResultType = typeof(TResult),
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, Task<TResult>> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = async (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8), typeof(T9), typeof(T10), typeof(T11), typeof(T12), typeof(T13));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                T7 p7 = (T7)parameters[6];
                T8 p8 = (T8)parameters[7];
                T9 p9 = (T9)parameters[8];
                T10 p10 = (T10)parameters[9];
                T11 p11 = (T11)parameters[10];
                T12 p12 = (T12)parameters[11];
                T13 p13 = (T13)parameters[12];
                return await handler(p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13);
            },
            ResultType = typeof(TResult),
        };
    }

    // 14 parameters

    /// <summary>
    /// Registers an action for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddAction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(string name, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8), typeof(T9), typeof(T10), typeof(T11), typeof(T12), typeof(T13), typeof(T14));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                T7 p7 = (T7)parameters[6];
                T8 p8 = (T8)parameters[7];
                T9 p9 = (T9)parameters[8];
                T10 p10 = (T10)parameters[9];
                T11 p11 = (T11)parameters[10];
                T12 p12 = (T12)parameters[11];
                T13 p13 = (T13)parameters[12];
                T14 p14 = (T14)parameters[13];
                handler(p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14);
                return Task.FromResult<object>(null);
            }
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TResult> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8), typeof(T9), typeof(T10), typeof(T11), typeof(T12), typeof(T13), typeof(T14));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                T7 p7 = (T7)parameters[6];
                T8 p8 = (T8)parameters[7];
                T9 p9 = (T9)parameters[8];
                T10 p10 = (T10)parameters[9];
                T11 p11 = (T11)parameters[10];
                T12 p12 = (T12)parameters[11];
                T13 p13 = (T13)parameters[12];
                T14 p14 = (T14)parameters[13];
                return Task.FromResult<object>(handler(p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14));
            },
            ResultType = typeof(TResult),
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, Task<TResult>> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = async (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8), typeof(T9), typeof(T10), typeof(T11), typeof(T12), typeof(T13), typeof(T14));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                T7 p7 = (T7)parameters[6];
                T8 p8 = (T8)parameters[7];
                T9 p9 = (T9)parameters[8];
                T10 p10 = (T10)parameters[9];
                T11 p11 = (T11)parameters[10];
                T12 p12 = (T12)parameters[11];
                T13 p13 = (T13)parameters[12];
                T14 p14 = (T14)parameters[13];
                return await handler(p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14);
            },
            ResultType = typeof(TResult),
        };
    }

    // 15 parameters

    /// <summary>
    /// Registers an action for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddAction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(string name, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8), typeof(T9), typeof(T10), typeof(T11), typeof(T12), typeof(T13), typeof(T14), typeof(T15));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                T7 p7 = (T7)parameters[6];
                T8 p8 = (T8)parameters[7];
                T9 p9 = (T9)parameters[8];
                T10 p10 = (T10)parameters[9];
                T11 p11 = (T11)parameters[10];
                T12 p12 = (T12)parameters[11];
                T13 p13 = (T13)parameters[12];
                T14 p14 = (T14)parameters[13];
                T15 p15 = (T15)parameters[14];
                handler(p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15);
                return Task.FromResult<object>(null);
            }
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TResult> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8), typeof(T9), typeof(T10), typeof(T11), typeof(T12), typeof(T13), typeof(T14), typeof(T15));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                T7 p7 = (T7)parameters[6];
                T8 p8 = (T8)parameters[7];
                T9 p9 = (T9)parameters[8];
                T10 p10 = (T10)parameters[9];
                T11 p11 = (T11)parameters[10];
                T12 p12 = (T12)parameters[11];
                T13 p13 = (T13)parameters[12];
                T14 p14 = (T14)parameters[13];
                T15 p15 = (T15)parameters[14];
                return Task.FromResult<object>(handler(p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15));
            },
            ResultType = typeof(TResult),
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, Task<TResult>> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = async (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8), typeof(T9), typeof(T10), typeof(T11), typeof(T12), typeof(T13), typeof(T14), typeof(T15));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                T7 p7 = (T7)parameters[6];
                T8 p8 = (T8)parameters[7];
                T9 p9 = (T9)parameters[8];
                T10 p10 = (T10)parameters[9];
                T11 p11 = (T11)parameters[10];
                T12 p12 = (T12)parameters[11];
                T13 p13 = (T13)parameters[12];
                T14 p14 = (T14)parameters[13];
                T15 p15 = (T15)parameters[14];
                return await handler(p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15);
            },
            ResultType = typeof(TResult),
        };
    }

    // 16 parameters

    /// <summary>
    /// Registers an action for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddAction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(string name, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8), typeof(T9), typeof(T10), typeof(T11), typeof(T12), typeof(T13), typeof(T14), typeof(T15), typeof(T16));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                T7 p7 = (T7)parameters[6];
                T8 p8 = (T8)parameters[7];
                T9 p9 = (T9)parameters[8];
                T10 p10 = (T10)parameters[9];
                T11 p11 = (T11)parameters[10];
                T12 p12 = (T12)parameters[11];
                T13 p13 = (T13)parameters[12];
                T14 p14 = (T14)parameters[13];
                T15 p15 = (T15)parameters[14];
                T16 p16 = (T16)parameters[15];
                handler(p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15, p16);
                return Task.FromResult<object>(null);
            }
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, TResult> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8), typeof(T9), typeof(T10), typeof(T11), typeof(T12), typeof(T13), typeof(T14), typeof(T15), typeof(T16));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                T7 p7 = (T7)parameters[6];
                T8 p8 = (T8)parameters[7];
                T9 p9 = (T9)parameters[8];
                T10 p10 = (T10)parameters[9];
                T11 p11 = (T11)parameters[10];
                T12 p12 = (T12)parameters[11];
                T13 p13 = (T13)parameters[12];
                T14 p14 = (T14)parameters[13];
                T15 p15 = (T15)parameters[14];
                T16 p16 = (T16)parameters[15];
                return Task.FromResult<object>(handler(p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15, p16));
            },
            ResultType = typeof(TResult),
        };
    }

    /// <summary>
    /// Registers a function for use with <code>galdrInvoke</code>.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="handler">The method to handle the action.</param>
    public void AddFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, Task<TResult>> handler)
    {
        _commands[name] = new CommandInfo
        {
            Handler = async (json) =>
            {
                object[] parameters = ParseParameters(json, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8), typeof(T9), typeof(T10), typeof(T11), typeof(T12), typeof(T13), typeof(T14), typeof(T15), typeof(T16));

                T1 p1 = (T1)parameters[0];
                T2 p2 = (T2)parameters[1];
                T3 p3 = (T3)parameters[2];
                T4 p4 = (T4)parameters[3];
                T5 p5 = (T5)parameters[4];
                T6 p6 = (T6)parameters[5];
                T7 p7 = (T7)parameters[6];
                T8 p8 = (T8)parameters[7];
                T9 p9 = (T9)parameters[8];
                T10 p10 = (T10)parameters[9];
                T11 p11 = (T11)parameters[10];
                T12 p12 = (T12)parameters[11];
                T13 p13 = (T13)parameters[12];
                T14 p14 = (T14)parameters[13];
                T15 p15 = (T15)parameters[14];
                T16 p16 = (T16)parameters[15];
                return await handler(p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15, p16);
            },
            ResultType = typeof(TResult),
        };
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
        Galdr galdr = new Galdr(new GaldrOptions()
        {
            Commands = _commands,
            ContentProvider = _contentProvider,
            Debug = _debug,
            Height = _height,
            InitScript = _initScript,
            LoadingBackground = _loadingBackground,
            LoadingMessage = _loadingMessage,
            MinHeight = _minHeight,
            MinWidth = _minWidth,
            Port = _port,
            Services = _services,
            ShowLoading = _showLoading,
            SpellCheckingLanguages = _spellCheckLanguages,
            Title = _title,
            Width = _width,
        });

        _serviceProvider = _services
            .AddSingleton(_ => new EventService(galdr.Webview))
            .AddSingleton<IEventService, EventService>()
            .BuildServiceProvider();

        return galdr;
    }

    #endregion

    #region Private Methods

    private static int SkipToValue(string content, int start)
    {
        // Skip past "key": to get to the value
        bool inQuotes = false;
        for (int i = start; i < content.Length; i++)
        {
            if (content[i] == '"' && (i == 0 || content[i - 1] != '\\'))
                inQuotes = !inQuotes;
            else if (!inQuotes && content[i] == ':')
                return i + 1;
        }
        return content.Length;
    }

    private object[] ParseParameters(string json, params Type[] parameterTypes)
    {
        var args = new object[parameterTypes.Length];
        Dictionary<string, object> jsonObject = null;
        int jsonParameterIndex = 0;

        for (int i = 0; i < parameterTypes.Length; i++)
        {
            if (_registeredServiceTypes.Contains(parameterTypes[i]))
            {
                // Resolve from DI
                args[i] = _serviceProvider.GetRequiredService(parameterTypes[i]);
            }
            else
            {
                // Parse from JSON - lazy initialize the JSON parsing
                jsonObject ??= ParseJsonToObject(json);

                string propertyName = $"p{jsonParameterIndex}";
                args[i] = DeserializeValue(jsonObject[propertyName], parameterTypes[i]);
                jsonParameterIndex++;
            }
        }

        return args;
    }

    private Dictionary<string, object> ParseJsonToObject(string json)
    {
        var result = new Dictionary<string, object>();
        string trimmed = json.Trim();

        // Handle empty JSON
        if (string.IsNullOrEmpty(trimmed) || trimmed == "{}")
            return result;

        if (!trimmed.StartsWith('{') || !trimmed.EndsWith('}'))
            throw new ArgumentException("Invalid JSON object");

        // Remove outer braces
        string content = trimmed.Substring(1, trimmed.Length - 2);
        int i = 0;
        int propertyIndex = 0;

        while (i < content.Length)
        {
            // Skip whitespace
            while (i < content.Length && char.IsWhiteSpace(content[i])) i++;
            if (i >= content.Length) break;

            // Skip past the key (we'll use positional names)
            i = SkipToValue(content, i);
            if (i >= content.Length) break;

            // Extract the value as raw JSON
            string rawValue = ExtractRawJsonValue(content, ref i);
            result[$"p{propertyIndex}"] = rawValue;
            propertyIndex++;

            // Skip comma if present
            while (i < content.Length && (char.IsWhiteSpace(content[i]) || content[i] == ',')) i++;
        }

        return result;
    }

    private object DeserializeValue(object rawJsonValue, Type targetType)
    {
        string jsonValue = (string)rawJsonValue;

        // Handle null
        if (string.IsNullOrEmpty(jsonValue) || jsonValue == "null")
        {
            return GetDefaultValue(targetType);
        }

        // Try primitives first (fastest path)
        if (TryParsePrimitive(jsonValue, targetType, out object primitiveValue))
        {
            return primitiveValue;
        }

        // Try generated deserializers
        if (TryDeserializeWithGenerated(jsonValue, targetType, out object deserializedValue))
        {
            return deserializedValue;
        }

        // Fallback for unsupported types
        throw new NotSupportedException($"Cannot deserialize type {targetType.Name}");
    }

    private bool TryParsePrimitive(string value, Type targetType, out object result)
    {
        result = null;

        // Remove quotes from string values
        if (targetType == typeof(string))
        {
            result = value.StartsWith('"') && value.EndsWith('"')
                ? value.Substring(1, value.Length - 2)
                : value;
            return true;
        }

        if (targetType == typeof(int) && int.TryParse(value, out int intVal))
        {
            result = intVal;
            return true;
        }

        if (targetType == typeof(long) && long.TryParse(value, out long longVal))
        {
            result = longVal;
            return true;
        }

        if (targetType == typeof(double) && double.TryParse(value, out double doubleVal))
        {
            result = doubleVal;
            return true;
        }

        if (targetType == typeof(decimal) && decimal.TryParse(value, out decimal decimalVal))
        {
            result = decimalVal;
            return true;
        }

        if (targetType == typeof(float) && float.TryParse(value, out float floatVal))
        {
            result = floatVal;
            return true;
        }

        if (targetType == typeof(bool) && bool.TryParse(value, out bool boolVal))
        {
            result = boolVal;
            return true;
        }

        if (targetType == typeof(DateTime) && DateTime.TryParse(value.Trim('"'), out DateTime dateVal))
        {
            result = dateVal;
            return true;
        }

        if (targetType == typeof(Guid) && Guid.TryParse(value.Trim('"'), out Guid guidVal))
        {
            result = guidVal;
            return true;
        }

        if (targetType.IsEnum && Enum.TryParse(targetType, value.Trim('"'), true, out object enumVal))
        {
            result = enumVal;
            return true;
        }

        return false;
    }

    private bool TryDeserializeWithGenerated(string jsonValue, Type targetType, out object result)
    {
        try
        {
            return GaldrJsonSerializerRegistry.TryDeserialize(jsonValue, targetType, out result);
        }
        catch
        {
            result = null;
            return false;
        }
    }

    private string ExtractRawJsonValue(string content, ref int index)
    {
        // Skip whitespace
        while (index < content.Length && char.IsWhiteSpace(content[index])) index++;

        if (index >= content.Length) return "";

        int start = index;

        // String value
        if (content[index] == '"')
        {
            index++; // Skip opening quote
            while (index < content.Length && (content[index] != '"' || content[index - 1] == '\\'))
                index++;
            index++; // Skip closing quote
            return content[start..index];
        }

        // Object value
        if (content[index] == '{')
        {
            int braceCount = 0;
            do
            {
                if (content[index] == '{') braceCount++;
                else if (content[index] == '}') braceCount--;
                index++;
            } while (index < content.Length && braceCount > 0);

            return content[start..index];
        }

        // Array value
        if (content[index] == '[')
        {
            int bracketCount = 0;
            do
            {
                if (content[index] == '[') bracketCount++;
                else if (content[index] == ']') bracketCount--;
                index++;
            } while (index < content.Length && bracketCount > 0);

            return content[start..index];
        }

        // Primitive value (number, boolean, null)
        while (index < content.Length && content[index] != ',' && content[index] != '}' && !char.IsWhiteSpace(content[index]))
            index++;

        return content[start..index].Trim();
    }

    private static object GetDefaultValue(Type type)
    {
        // Handle common value types explicitly
        if (type == typeof(int)) return 0;
        if (type == typeof(long)) return 0L;
        if (type == typeof(double)) return 0.0;
        if (type == typeof(decimal)) return 0m;
        if (type == typeof(float)) return 0f;
        if (type == typeof(bool)) return false;
        if (type == typeof(byte)) return (byte)0;
        if (type == typeof(short)) return (short)0;
        if (type == typeof(char)) return '\0';
        if (type == typeof(DateTime)) return default(DateTime);
        if (type == typeof(DateOnly)) return default(DateOnly);
        if (type == typeof(TimeOnly)) return default(TimeOnly);
        if (type == typeof(Guid)) return default(Guid);

        // Handle nullable value types
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            return null;

        // For reference types, return null
        if (!type.IsValueType)
            return null!;

        // For other value types (structs, enums), we can't create them without reflection
        // This should be rare since most common types are handled above
        throw new NotSupportedException($"Cannot create default value for type {type.Name} in AOT context");
    }

    #endregion
}

