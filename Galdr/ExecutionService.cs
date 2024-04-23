using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Galdr;

internal sealed class ExecutionService
{
    #region Fields

    private readonly IServiceProvider _serviceProvider;

    #endregion

    #region Constructor

    public ExecutionService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    #endregion

    #region Public Methods

    public async Task<object> ExecuteMethod(MethodInfo method, object[] args)
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
            else
            {
                obj = Activator.CreateInstance(method.DeclaringType);
                result = method.Invoke(obj, args);
            }
        }

        if (method.ReturnType == typeof(Task) || method.ReturnType.BaseType == typeof(Task))
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
            else
            {
                result = null;
            }
        }

        return result;
    }

    public object[] ExtractArguments(MethodInfo method, IEnumerable<object> parameters)
    {
        List<object> args = new();

        ParameterInfo[] delegateParameters = method.GetParameters();
        JObject jsonObject = parameters?.FirstOrDefault() as JObject;

        foreach (ParameterInfo param in delegateParameters)
        {
            object parameter = null;

            if (jsonObject?.ContainsKey(param.Name) == true)
            {
                parameter = jsonObject.GetValue(param.Name).ToObject(param.ParameterType);
            }

            if (parameter == null)
            {
                parameter = _serviceProvider.GetService(param.ParameterType);
            }

            if (parameter == null)
            {
                parameter = jsonObject?.ToObject(param.ParameterType);
            }

            if (parameter != null)
            {
                args.Add(parameter);
            }
        }

        return args.ToArray();
    }

    #endregion
}
