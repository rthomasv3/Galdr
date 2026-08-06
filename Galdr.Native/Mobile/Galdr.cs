using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using GaldrJson;
using Microsoft.Extensions.DependencyInjection;

namespace Galdr.Native;

/// <summary>
/// Mobile host for a Galdr application. Exposes the same public surface as the desktop
/// host so application code compiles unchanged; window-oriented members are no-ops
/// because mobile apps do not own a window.
/// </summary>
[GaldrJsonIgnore]
public class Galdr : IDisposable
{
    #region Fields

    private readonly GaldrOptions _options;
    private readonly Dictionary<string, CommandInfo> _commands;
    private readonly IGaldrJsonSerializer _galdrJsonSerializer;
    private readonly GaldrJsonOptions _galdrJsonOptions;
    private readonly bool _debug;

    private IGaldrMobileShell _shell;
    private IServiceProvider _serviceProvider;
    private UnhandledExceptionEventHandler _appDomainExceptionHandler;
    private EventHandler<UnobservedTaskExceptionEventArgs> _unobservedTaskExceptionHandler;
    private bool _backgrounded;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new instance of the <see cref="Galdr"/> class.
    /// </summary>
    /// <remarks>
    /// Construction is side-effect-free — services are not built until <see cref="Run"/>
    /// is called.
    /// </remarks>
    public Galdr(GaldrOptions options)
    {
        _options = options;
        _commands = options.Commands;
        _debug = options.Debug;
        _galdrJsonSerializer = options.GaldrJsonSerializer;
        _galdrJsonOptions = options.GaldrJsonOptions;
    }

    #endregion

    #region Properties

    internal GaldrOptions Options => _options;

    internal IServiceProvider ServiceProvider => _serviceProvider;

    internal IGaldrMobileShell Shell
    {
        get => _shell;
        set => _shell = value;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Runs the application. On Android the OS already owns the main loop, so this call
    /// fires the startup hooks, builds services, hands the configured app to the platform
    /// shell (which creates the webview), and returns without blocking. On iOS this call
    /// enters <c>UIApplication.Main</c> — the native main loop — and blocks for the
    /// lifetime of the app, exactly like desktop.
    /// </summary>
    public Galdr Run()
    {
        SubscribeUnhandledExceptionHooks();

        _options.BeforeStartup?.Invoke();

        BuildServiceProvider();

#if ANDROID
        GaldrActivity.Handoff(this);

        OnShellAttached();

        return this;
#elif IOS
        GaldrAppDelegate.Current = this;

        // Never returns — the process exits from inside the loop.
        UIKit.UIApplication.Main(Environment.GetCommandLineArgs(), null, typeof(GaldrAppDelegate));

        return this;
#else
        throw new PlatformNotSupportedException(
            "The Galdr mobile shell for this platform is not implemented yet.");
#endif
    }

    /// <summary>
    /// No-op on mobile — there is no native window handle concept. Always returns
    /// <see cref="IntPtr.Zero"/>.
    /// </summary>
    public IntPtr GetWindow()
    {
        return IntPtr.Zero;
    }

    /// <summary>
    /// No-op on mobile. Always returns <see cref="IntPtr.Zero"/>.
    /// </summary>
    public IntPtr GetNativeHandle(WebviewNativeHandleKind kind)
    {
        return IntPtr.Zero;
    }

    /// <summary>
    /// No-op on mobile — mobile apps have no window title.
    /// </summary>
    public void SetTitle(string title)
    {
    }

    /// <summary>
    /// No-op on mobile — the OS owns the window geometry.
    /// </summary>
    public void SetSize(int width, int height, WebviewHint hint)
    {
    }

    /// <summary>
    /// No-op on mobile — the OS owns the window geometry.
    /// </summary>
    public void SetPosition(int x, int y)
    {
    }

    /// <summary>
    /// No-op on mobile — the OS owns the window state.
    /// </summary>
    public void SetWindowState(WindowState state)
    {
    }

    /// <summary>
    /// No-op on mobile — returns a zeroed <see cref="WindowSize"/>.
    /// </summary>
    public WindowSize GetSize()
    {
        return new WindowSize { Width = 0, Height = 0 };
    }

    /// <summary>
    /// No-op on mobile — always returns <c>null</c>.
    /// </summary>
    public WindowPosition? GetPosition()
    {
        return null;
    }

    /// <summary>
    /// No-op on mobile — always reports <see cref="WindowState.Normal"/>.
    /// </summary>
    public WindowState GetWindowState()
    {
        return WindowState.Normal;
    }

    /// <summary>
    /// Evaluates arbitrary JavaScript code. Evaluation happens asynchronously on the
    /// UI thread, and the result of the expression is ignored.
    /// </summary>
    public void Evaluate(string javascript)
    {
        IGaldrMobileShell shell = _shell;

        if (shell != null)
        {
            shell.RunOnUiThread(() => shell.EvaluateJavascript(javascript));
        }
    }

    /// <summary>
    /// Posts a function to be executed on the platform's main/UI thread. Silently no-ops
    /// if called before <see cref="Run"/> has attached the platform shell.
    /// </summary>
    public void Dispatch(Action dispatchFunc)
    {
        _shell?.RunOnUiThread(dispatchFunc);
    }

    /// <summary>
    /// No-op on mobile — apps do not terminate themselves; the OS owns the process lifetime.
    /// </summary>
    public void Terminate()
    {
    }

    /// <summary>
    /// No-op on mobile aside from releasing exception hooks. On desktop this runs when
    /// Main exits after the main loop ends; on mobile Main returns while the app is
    /// still running, so teardown is owned by the OS lifecycle instead.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// Executes a galdrInvoke command and resolves it back into the page. Mirrors the
    /// desktop command pipeline: results serialize through GaldrJson, errors return the
    /// <see cref="RPCMessage"/> shape and fire the OnCommandError hook.
    /// </summary>
    internal async Task HandleCommand(string id, string commandName, string argsJson)
    {
        try
        {
            if (_commands.TryGetValue(commandName, out CommandInfo commandInfo))
            {
                object result = await commandInfo.Handler(argsJson ?? "{}");

                if (result == null)
                {
                    Respond(id, isError: false, "");
                }
                else
                {
                    if (_galdrJsonSerializer.TrySerialize(result, commandInfo.ResultType, out string json, _galdrJsonOptions))
                    {
                        Respond(id, isError: false, json);
                    }
                    else
                    {
                        Respond(id, isError: false, result.ToString());
                    }
                }
            }
        }
        catch (Exception ex)
        {
            string json = _galdrJsonSerializer.Serialize(new RPCMessage
            {
                Message = _debug ? ex.ToString() : ex.Message
            });
            Respond(id, isError: true, json);

            if (_options.OnCommandError != null)
            {
                try
                {
                    _options.OnCommandError(
                        new CommandErrorContext
                        {
                            CommandName = commandName,
                            Exception = ex,
                        },
                        _serviceProvider);
                }
                catch
                {
                    // Swallow — the error hook must not disrupt the error response to the frontend.
                }
            }
        }
    }

    /// <summary>
    /// Fires the startup hooks once the platform shell has created the webview and
    /// attached itself. On Android this runs inline inside <see cref="Run"/>; on iOS
    /// the view controller calls it after the scene connects.
    /// </summary>
    internal void OnShellAttached()
    {
        _options.Startup?.Invoke(_serviceProvider);

        if (_options.AfterStartup != null)
        {
            Dispatch(() => _options.AfterStartup(_serviceProvider));
        }
    }

    /// <summary>
    /// Fires the OnBackground hook. Called by the platform shell when the app is backgrounded.
    /// </summary>
    internal void InvokeBackground()
    {
        _backgrounded = true;

        if (_serviceProvider != null)
        {
            _options.Background?.Invoke(_serviceProvider);
        }
    }

    /// <summary>
    /// Fires the OnResume hook. Called by the platform shell on foreground return; the
    /// initial launch does not count as a resume.
    /// </summary>
    internal void InvokeResume()
    {
        if (_backgrounded && _serviceProvider != null)
        {
            _backgrounded = false;
            _options.Resume?.Invoke(_serviceProvider);
        }
    }

    /// <summary>JSON string literal with escaping, for embedding values in evaluated scripts.</summary>
    internal static string Quote(string value)
    {
        StringBuilder sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (char c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20)
                    {
                        sb.Append("\\u").Append(((int)c).ToString("x4"));
                    }
                    else
                    {
                        sb.Append(c);
                    }
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    #endregion

    #region Private Methods

    private void Respond(string id, bool isError, string resultJson)
    {
        // The result travels as a JS string literal and is JSON.parse'd by the bootstrap.
        string script = $"window.__galdrResolve({Quote(id)}, {(isError ? "true" : "false")}, {Quote(resultJson)})";
        Evaluate(script);
    }

    private void BuildServiceProvider()
    {
        DialogService dialogService = new();
        EventService eventService = new(this);

        _serviceProvider = _options.Services
            .AddSingleton<IEventService>(eventService)
            .AddSingleton(eventService)
            .AddSingleton<IDialogService>(dialogService)
            .AddSingleton(dialogService)
            .AddSingleton(this)
            .BuildServiceProvider();

        if (_options.ServiceProviderAccessor != null)
        {
            _options.ServiceProviderAccessor.Provider = _serviceProvider;
        }
    }

    private void SubscribeUnhandledExceptionHooks()
    {
        if (_options.OnUnhandledException != null)
        {
            _appDomainExceptionHandler = HandleAppDomainException;
            _unobservedTaskExceptionHandler = HandleUnobservedTaskException;

            AppDomain.CurrentDomain.UnhandledException += _appDomainExceptionHandler;
            TaskScheduler.UnobservedTaskException += _unobservedTaskExceptionHandler;
        }
    }

    private void HandleAppDomainException(object sender, UnhandledExceptionEventArgs e)
    {
        Exception ex = e.ExceptionObject as Exception;

        InvokeUnhandledExceptionHook(new UnhandledExceptionContext
        {
            Exception = ex,
            IsTerminating = e.IsTerminating,
            Source = UnhandledExceptionSource.AppDomain,
        });
    }

    private void HandleUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        InvokeUnhandledExceptionHook(new UnhandledExceptionContext
        {
            Exception = e.Exception,
            IsTerminating = false,
            Source = UnhandledExceptionSource.TaskScheduler,
        });

        e.SetObserved();
    }

    private void InvokeUnhandledExceptionHook(UnhandledExceptionContext context)
    {
        try
        {
            _options.OnUnhandledException(context, _serviceProvider);
        }
        catch
        {
            // Swallow — the hook must not raise new exceptions during shutdown or from
            // a secondary task scheduler event, which could cause recursive reentry.
        }
    }

    #endregion
}
