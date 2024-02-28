using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SharpWebview;

namespace Galdr;

public class Galdr : IDisposable
{
    #region Fields

    private readonly Webview _webView;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, MethodInfo> _commands;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new instance of the <see cref="Galdr"/> class.
    /// </summary>
    /// <remarks>
    /// Requires the threading model for the application to be single-threaded apartment (<see cref="STAThreadAttribute"/>).
    /// </remarks>
    /// <exception cref="NullReferenceException">
    /// </exception>
    /// <exception cref="AccessViolationException">
    /// Thrown when the threading model for the application is not single-threaded apartment (<see cref="STAThreadAttribute"/>).
    /// </exception>
    public Galdr(GaldrOptions options)
    {
        options.Services.AddSingleton(this);
        _serviceProvider = options.Services.BuildServiceProvider();

        _commands = options.Commands;

        _webView = new(options.Debug, true);

        _webView
            .SetTitle(options.Title)
            .SetSize(options.Width, options.Height, WebviewHint.None)
            .SetSize(options.MinWidth, options.MinHeight, WebviewHint.Min)
            .Bind("galdrInvoke", HandleCommand);

        _webView.Navigate(options.Content);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Runs the main loop of the <see cref="Webview"/>.
    /// </summary>
    public void Run()
    {
        _webView.Run();
    }

    /// <summary>
    /// Dispatches a new CustomEvent of the given name on the main window and thread.
    /// </summary>
    public void PublishEvent<T>(string eventName, T args)
    {
        string js = $"window.dispatchEvent(new CustomEvent('{eventName}', {{ detail: {JsonConvert.SerializeObject(args)} }}));";
        _webView.Dispatch(() => _webView.Evaluate(js));
    }

    /// <summary>
    /// Opens a system directory selection dialog.
    /// </summary>
    /// <returns>
    /// The path of the directory or null if cancelled.
    /// </returns>
    public async Task<string> OpenDirectoryDialog()
    {
        return await Task.Run(() =>
        {
            string directory = null;

            NativeFileDialogSharp.DialogResult result = NativeFileDialogSharp.Dialog.FolderPicker();

            if (result.IsOk)
            {
                directory = result.Path;
            }

            return directory;
        });
    }

    /// <summary>
    /// Opens a system file selection dialog.
    /// </summary>
    /// <returns>
    /// The path of the file or null if cancelled.
    /// </returns>
    public async Task<string> OpenFileDialog()
    {
        return await Task.Run(() =>
        {
            string file = null;

            NativeFileDialogSharp.DialogResult result = NativeFileDialogSharp.Dialog.FileOpen();

            if (result.IsOk)
            {
                file = result.Path;
            }

            return file;
        });
    }

    /// <summary>
    /// Opens a system file save dialog.
    /// </summary>
    /// <returns>
    /// The path of the file or null if cancelled.
    /// </returns>
    public async Task<string> OpenSaveDialog()
    {
        return await Task.Run(() =>
        {
            string file =null;

            NativeFileDialogSharp.DialogResult result = NativeFileDialogSharp.Dialog.FileSave();

            if (result.IsOk)
            {
                file = result.Path;
            }

            return file;
        });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _webView.Dispose();
    }

    #endregion

    #region Private Methods

    private async void HandleCommand(string id, string paramString)
    {
        object[] parameters = JsonConvert.DeserializeObject<object[]>(paramString);

        if (parameters.Length > 0)
        {
            string commandName = parameters[0].ToString();

            if (!String.IsNullOrWhiteSpace(commandName) && _commands.ContainsKey(commandName))
            {
                try
                {
                    MethodInfo method = _commands[commandName];

                    object[] args = ExtractArguments(method, parameters.Skip(1));
                    object result = await ExecuteMethod(method, args);

                    if (result != null)
                    {
                        _webView.Return(id, RPCResult.Success, JsonConvert.SerializeObject(result));
                    }
                }
                catch (Exception e)
                {
                    _webView.Return(id, RPCResult.Error, JsonConvert.SerializeObject(e));
                }
            }
        }
    }

    private async Task<object> ExecuteMethod(MethodInfo method, object[] args)
    {
        object result = null;

        if (method.IsStatic)
        {
            result = method.Invoke(null, args);
        }
        else
        {
            object obj = _serviceProvider.GetService(method.DeclaringType);

            if (obj != null)
            {
                result = method.Invoke(obj, args);
            }
        }

        if (method.ReturnType.BaseType == typeof(Task))
        {
            Task task = result as Task;

            await task;

            if (method.ReturnType.IsGenericType)
            {
                PropertyInfo[] properties = method.ReturnType.GetProperties();

                foreach (PropertyInfo property in properties)
                {
                    if (property.Name == "Result")
                    {
                        result = property.GetValue(result);
                        break;
                    }
                }
            }
        }

        return result;
    }

    private object[] ExtractArguments(MethodInfo method, IEnumerable<object> parameters)
    {
        List<object> args = new();

        ParameterInfo[] delegateParameters = method.GetParameters();
        JObject jsonObject = parameters.FirstOrDefault() as JObject;

        foreach (ParameterInfo param in delegateParameters)
        {
            if (param.ParameterType.IsPrimitive || param.ParameterType == typeof(string))
            {
                if (jsonObject?.ContainsKey(param.Name) == true)
                {
                    args.Add(jsonObject.GetValue(param.Name).ToObject(param.ParameterType));
                }
            }
            else
            {
                object parameter = _serviceProvider.GetService(param.ParameterType);

                if (parameter != null)
                {
                    args.Add(parameter);
                }
                else
                {
                    parameter = jsonObject?.ToObject(param.ParameterType);

                    if (parameter != null)
                    {
                        args.Add(parameter);
                        jsonObject = null;
                    }
                }
            }
        }

        return args.ToArray();
    }

    #endregion
}
