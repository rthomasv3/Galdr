using System;
using System.IO;
using Galdr.Native;
using GaldrApp.Services;
using GaldrApp.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace GaldrApp;

internal class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Config config = Config.Create("GaldrApp");

        GaldrBuilder builder = new GaldrBuilder()
            .SetTitle("GaldrApp")
            .SetSize(1024, 768)
            .SetMinSize(640, 480)
            .AddSingleton(config)
            .AddSingleton<ILoggingService, FileLoggingService>()
            .OnCommandError((context, serviceProvider) =>
            {
                ILoggingService logger = serviceProvider.GetService<ILoggingService>();

                if (logger != null)
                {
                    string source = string.IsNullOrEmpty(context.CommandName) ? "galdrInvoke" : context.CommandName;
                    logger.Error(source, "Command handler threw", context.Exception);
                }
            })
            .OnUnhandledException((context, serviceProvider) =>
            {
                ILoggingService logger = serviceProvider?.GetService<ILoggingService>();

                if (logger != null)
                {
                    string source = context.Source == UnhandledExceptionSource.AppDomain ? "AppDomain" : "TaskScheduler";
                    string message = context.IsTerminating
                        ? "Unhandled exception - process is terminating"
                        : "Unhandled exception (non-terminating)";

                    logger.Error(source, message, context.Exception);
                }
            });

//-:cnd:noEmit
#if DEBUG
        builder.SetDebug(true)
               .SetContentProvider(new UrlContent("http://localhost:5174"));
#else
        string wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        builder.SetContentProvider(new FolderContent(wwwroot));
#endif
//+:cnd:noEmit

        using Galdr.Native.Galdr galdr = builder.Build().Run();
    }
}
